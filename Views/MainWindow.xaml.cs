using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Launcher.Models;
using Launcher.Services;
using Launcher.Themes;
using Launcher.ViewModels;

namespace Launcher.Views;

/// <summary>
/// Code-behind for the launcher window. Owns the open/close animations and
/// keyboard routing (Esc, Up/Down, Enter); the visual tree and data-binding
/// do the rest. Colors/sizes come from ThemeService via DynamicResource in
/// XAML; the handful of values that can't be plain resources (Window.Width,
/// which also needs the glow margin baked in, and Storyboard durations) are
/// read directly from ThemeService.Current each time they're needed, so a
/// theme change is picked up on the very next open/close with no restart.
/// </summary>
public partial class MainWindow : Window
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private readonly MainViewModel _viewModel;
    private readonly AppSettings _settings;
    private readonly ThemeService _themeService;

    /// <summary>Last measured window height (DIPs), used to position the window on the
    /// correct monitor before its real content height is known for this show.</summary>
    private double _lastKnownHeight;

    /// <summary>Drives the "Glitch" ambient pattern's irregular burst timing.</summary>
    private DispatcherTimer? _glitchTimer;
    private readonly Random _glitchRandom = new();

    public MainWindow(MainViewModel viewModel, AppSettings settings, ThemeService themeService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _settings = settings;
        _themeService = themeService;
        DataContext = _viewModel;

        ApplyWindowWidthFromTheme();
        _lastKnownHeight = _themeService.Current.WindowMinHeight + ComputeGlowMargin() * 2;

        // A theme edit (including a live preview from Settings) can change the
        // window width or glow margin; keep Window.Width in sync immediately.
        // It can also change (or turn on/off) the ambient animation, so restart
        // that too — but only if the bar is actually open right now.
        _themeService.ThemeChanged += _ => Dispatcher.Invoke(() =>
        {
            ApplyWindowWidthFromTheme();
            UpdateRootClip();
            if (Visibility == Visibility.Visible)
                StartAmbientAnimation();
        });

        // Border's "clip content to rounded corners" behavior only covers the
        // background/border stroke and ordinary layout content — it does NOT
        // reliably contain a child whose RenderTransform moves it beyond the
        // Border's own bounds (exactly what the Shimmer ambient animation does).
        // An explicit rounded Clip on RootBorder itself guarantees everything
        // inside it — including that sweep — stays within the rounded shape.
        RootBorder.SizeChanged += (_, _) => UpdateRootClip();

        PreviewKeyDown += OnPreviewKeyDown;
        Deactivated += (_, _) => AnimateHide();
        ResultsList.MouseUp += (_, _) => _viewModel.LaunchSelectedCommand.Execute(null);

        // On a cold start, calling Keyboard.Focus(SearchBox) synchronously right
        // after Visibility=Visible can lose the race: the very first show does
        // more work under the hood than every later one - the window's own
        // Loaded event, first-time style/template realization, and the first
        // real layout/render pass all fire for the first time and are queued
        // at Loaded/Render/Normal dispatcher priority, all of which run BEFORE
        // DispatcherPriority.Input. So a focus request queued at Input can
        // still lose to that first-time churn even though it never competes
        // with anything on later opens. ContextIdle is lower than all of
        // those, so this reliably runs dead last, after everything the first
        // show does - re-asserted on every Activated (fires every AnimateShow,
        // not just the first) so it's a no-op safety net on later opens.
        Activated += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        }), DispatcherPriority.ContextIdle);
    }

    /// <summary>
    /// Forces this window's Win32 handle, styles/templates, and first layout
    /// pass to be created right now, invisibly, instead of lazily on the first
    /// real AnimateShow. WPF only does that heavier one-time setup the very
    /// first time a window's Visibility becomes Visible - showing it for real
    /// on the very first user-triggered Ctrl+Space made that first show behave
    /// differently from every later one (wrong monitor/position, and
    /// intermittently no real keyboard focus even though later opens were
    /// always fine). Calling this once at startup, before the user can ever
    /// press the hotkey, makes the first real show already "warm".
    /// </summary>
    public void WarmUp()
    {
        // Park it far off-screen so nothing is visible even for a frame -
        // Opacity is already 0 by default (see XAML) but this is a second,
        // independent guarantee that isn't relying on that.
        Left = -32000;
        Top = -32000;
        Visibility = Visibility.Visible;
        UpdateLayout();
        Visibility = Visibility.Hidden;
    }

    /// <summary>
    /// WPF's plain Activate() calls SetForegroundWindow internally, but Windows
    /// only honors that call unconditionally for the process that currently
    /// "owns" the input focus - a background process (which is exactly what we
    /// are: our own message-only hotkey window received WM_HOTKEY, not this
    /// window) can have that request silently ignored, especially while a
    /// browser or File Explorer - both of which do their own input-focus/IME
    /// juggling - holds the foreground. AttachThreadInput temporarily merges
    /// our thread's input state with the current foreground thread's, which is
    /// the standard, documented way to make SetForegroundWindow succeed
    /// unconditionally regardless of who currently owns focus.
    /// </summary>
    private void ForceForeground()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            var foregroundHwnd = GetForegroundWindow();
            var foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, IntPtr.Zero);
            var thisThreadId = GetCurrentThreadId();

            var attached = foregroundThreadId != 0 && foregroundThreadId != thisThreadId
                && AttachThreadInput(thisThreadId, foregroundThreadId, true);
            try
            {
                Activate();
                SetForegroundWindow(hwnd);

                // Belt-and-braces: some windows (certain browser/Explorer surfaces)
                // sit in their own topmost band that a plain Activate()/
                // SetForegroundWindow doesn't outrank. Toggling Topmost off/on
                // re-asserts our place at the very front of the z-order.
                Topmost = false;
                Topmost = true;
            }
            finally
            {
                if (attached)
                    AttachThreadInput(thisThreadId, foregroundThreadId, false);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("ForceForeground failed; falling back to plain Activate().", ex);
            Activate();
        }
    }

    private double ComputeGlowMargin() => ThemeService.ComputeGlowMargin(_themeService.Current);

    /// <summary>
    /// Recomputes RootBorder's explicit rounded-rectangle Clip to match its
    /// current size and the theme's corner radius. Called on size changes and
    /// theme changes so the clip never lags behind either.
    /// </summary>
    private void UpdateRootClip()
    {
        if (RootBorder.ActualWidth <= 0 || RootBorder.ActualHeight <= 0)
            return;

        var radius = _themeService.Current.CornerRadius;
        RootBorder.Clip = new System.Windows.Media.RectangleGeometry(
            new Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight), radius, radius);
    }

    private void ApplyWindowWidthFromTheme()
    {
        Width = _themeService.Current.WindowWidth + ComputeGlowMargin() * 2;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                AnimateHide();
                e.Handled = true;
                break;
            case Key.Down:
                _viewModel.MoveSelectionDownCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Up:
                _viewModel.MoveSelectionUpCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter:
                _viewModel.LaunchSelectedCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Positions the window centered within <paramref name="workArea"/> (already in
    /// DIPs, for whichever monitor was resolved), using <paramref name="heightDip"/>
    /// as the window's current/expected height.
    /// </summary>
    private void PositionOnMonitor(MonitorWorkArea workArea, double heightDip)
    {
        Left = workArea.Left + (workArea.Width - Width) / 2.0;
        Top = workArea.Top + (workArea.Height * 0.32) - (heightDip / 2.0);
        if (Top < workArea.Top + 24) Top = workArea.Top + 24;
    }

    public void AnimateShow()
    {
        // Deliberately no "already animating" guard here: it used to gate re-entrant
        // calls via a manual _isAnimating flag reset only by the scale animation's
        // Completed callback — if an AnimateHide (e.g. from Deactivated, which fires
        // easily while another window like Settings is being interacted with) then
        // replaced that same animation mid-flight, the callback was silently dropped
        // and the flag got stuck true forever, permanently no-op'ing AnimateShow from
        // then on ("Ctrl+Space stops working"). WPF animations replace cleanly when
        // BeginAnimation is called again on the same property, so it's safe to just
        // always (re)start — Visibility (checked by AnimateHide) is the only state
        // that actually needs to stay consistent.
        _viewModel.ResetForShow();

        // Resolve the target monitor (the one containing the app the user was just
        // in) BEFORE this window becomes visible/activated, so it never briefly
        // renders on the primary monitor first.
        var workArea = MonitorHelper.GetTargetMonitorWorkArea();

        // Position using our best current estimate of height, then show. Opacity is
        // still 0 at this point (see XAML), so any tiny discrepancy here is invisible.
        PositionOnMonitor(workArea, _lastKnownHeight);

        Visibility = Visibility.Visible;
        UpdateLayout();
        UpdateRootClip();

        // Now that layout has run for the actual (empty) result state, refine the
        // position with the real height before anything becomes visible.
        _lastKnownHeight = ActualHeight;
        PositionOnMonitor(workArea, _lastKnownHeight);

        var theme = _themeService.Current;
        var animationsEnabled = theme.AnimationsEnabled;
        var duration = TimeSpan.FromMilliseconds(animationsEnabled ? Math.Max(30, theme.OpenDurationMs) : 1);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var scaleFrom = animationsEnabled ? theme.OpenScaleFrom : 1.0;
        var yFrom = animationsEnabled ? theme.OpenYOffset : 0.0;

        var opacityAnim = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
        var scaleAnim = new DoubleAnimation(scaleFrom, 1.0, duration) { EasingFunction = ease };
        var translateAnim = new DoubleAnimation(yFrom, 0, duration) { EasingFunction = ease };

        BeginAnimation(OpacityProperty, opacityAnim);
        ScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
        ScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
        TranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, translateAnim);

        ForceForeground();
        // Focus is (re)asserted from the Activated handler wired up in the
        // constructor - see the comment there for why an immediate synchronous
        // Keyboard.Focus() call here isn't reliable on the very first show.
        Keyboard.Focus(SearchBox);

        StartAmbientAnimation();
    }

    public void AnimateHide()
    {
        if (Visibility != Visibility.Visible)
            return;

        StopAmbientAnimation();

        var theme = _themeService.Current;
        var animationsEnabled = theme.AnimationsEnabled;
        var duration = TimeSpan.FromMilliseconds(animationsEnabled ? Math.Max(30, theme.CloseDurationMs) : 1);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        var scaleTo = animationsEnabled ? theme.OpenScaleFrom : 1.0;
        var yTo = animationsEnabled ? theme.OpenYOffset : 0.0;

        var opacityAnim = new DoubleAnimation(Opacity, 0, duration) { EasingFunction = ease };
        var scaleAnim = new DoubleAnimation(1.0, scaleTo, duration) { EasingFunction = ease };
        var translateAnim = new DoubleAnimation(0, yTo, duration) { EasingFunction = ease };

        opacityAnim.Completed += (_, _) => Visibility = Visibility.Hidden;

        BeginAnimation(OpacityProperty, opacityAnim);
        ScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
        ScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
        TranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, translateAnim);
    }

    /// <summary>
    /// Starts (or restarts, if already running) the theme's looping "ambient"
    /// animation — a subtle effect played inside the bar for as long as it's
    /// open, distinct from the one-shot open/close animation. "None" clears
    /// any running loop; "Pulse" breathes the glow's opacity; "Shimmer" sweeps
    /// a soft diagonal highlight across the bar; "Glitch" fires irregular short
    /// signal-jitter bursts (position + glow color flicker) — a more fitting
    /// pattern for the Cyberpunk preset than a smooth sweep. Purely decorative —
    /// none of these ever touch search/launch state.
    /// </summary>
    private void StartAmbientAnimation()
    {
        StopAmbientAnimation();

        var theme = _themeService.Current;
        if (!theme.AnimationsEnabled)
            return;

        var seconds = Math.Clamp(theme.AmbientAnimationSpeedSeconds, 1, 8);

        switch (theme.AmbientAnimationStyle)
        {
            case "Pulse":
            {
                var baseOpacity = theme.GlowEnabled ? theme.GlowOpacity : 0.0;
                var pulse = new DoubleAnimation(baseOpacity * 0.55, baseOpacity, TimeSpan.FromSeconds(seconds / 2.0))
                {
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                GlowEffect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, pulse);
                break;
            }
            case "Shimmer":
            {
                ShimmerOverlay.Opacity = 1;
                var sweepWidth = Math.Max(400, ActualWidth + 200);
                var sweep = new DoubleAnimation(-sweepWidth * 0.6, sweepWidth, TimeSpan.FromSeconds(seconds))
                {
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                    RepeatBehavior = RepeatBehavior.Forever
                };
                ShimmerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, sweep);
                break;
            }
            case "Glitch":
            {
                // AmbientAnimationSpeedSeconds is the AVERAGE gap between bursts here
                // (not a sweep duration) — each tick reschedules itself with a new
                // randomized interval, so the effect never feels metronomic.
                _glitchTimer = new DispatcherTimer { Interval = NextGlitchInterval(seconds) };
                _glitchTimer.Tick += (_, _) =>
                {
                    FireGlitchBurst(theme);
                    _glitchTimer!.Interval = NextGlitchInterval(seconds);
                };
                _glitchTimer.Start();
                break;
            }
        }
    }

    private TimeSpan NextGlitchInterval(double averageSeconds) =>
        TimeSpan.FromSeconds(averageSeconds * (0.5 + _glitchRandom.NextDouble()));

    /// <summary>
    /// A single ~100-150ms "signal glitch": a couple of sharp horizontal jumps
    /// (RootBorder's TranslateTransform.X — otherwise unused outside the Y-only
    /// open/close animation) plus a quick flash of the glow color toward the
    /// accent color, both settling back to normal. Deliberately abrupt/discrete
    /// rather than eased, since that reads as "glitchy" instead of "bouncy".
    /// </summary>
    private void FireGlitchBurst(ThemeDefinition theme)
    {
        double Jitter() => (_glitchRandom.NextDouble() - 0.5) * 10; // ~±5px

        var jumpX = new DoubleAnimationUsingKeyFrames();
        var t = TimeSpan.Zero;
        foreach (var offset in new[] { Jitter(), Jitter(), 0.0 })
        {
            jumpX.KeyFrames.Add(new DiscreteDoubleKeyFrame(offset, KeyTime.FromTimeSpan(t)));
            t += TimeSpan.FromMilliseconds(35 + _glitchRandom.Next(20));
        }
        TranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, jumpX);

        var glowFlash = new ColorAnimationUsingKeyFrames();
        var accent = ThemeService.ParseColorSafe(theme.AccentColor);
        var glow = ThemeService.ParseColorSafe(theme.GlowColor);
        glowFlash.KeyFrames.Add(new DiscreteColorKeyFrame(accent, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        glowFlash.KeyFrames.Add(new DiscreteColorKeyFrame(glow, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
        GlowEffect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty, glowFlash);

        // A brief opacity dip on the whole bar reads as a dropped-signal flicker.
        var flicker = new DoubleAnimationUsingKeyFrames();
        flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.82, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(30))));
        flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
        RootBorder.BeginAnimation(OpacityProperty, flicker);
    }

    private void StopAmbientAnimation()
    {
        _glitchTimer?.Stop();
        _glitchTimer = null;

        // Removing each animation (passing null) reverts the property to its base
        // value — still the DynamicResource-driven theme value for Glow/Opacity —
        // no extra bookkeeping needed to "give the property back" to the theme.
        GlowEffect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, null);
        GlowEffect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty, null);
        ShimmerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        TranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        RootBorder.BeginAnimation(OpacityProperty, null);
        ShimmerOverlay.Opacity = 0;
    }
}
