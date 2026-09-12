using System.Windows;
using UserControl = System.Windows.Controls.UserControl;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using ColorConverter = System.Windows.Media.ColorConverter;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.Themes;

namespace Launcher.Views;

/// <summary>
/// A compact, dependency-free HSV color picker: a saturation/value square, a
/// hue strip, a hex text box, and a row of preset swatches. No external
/// package — just a handful of gradient Borders and mouse-drag math. Hosted
/// inside a Popup by <see cref="SettingsWindow"/> so clicking any color
/// swatch in Settings opens this instead of requiring the user to type hex.
/// </summary>
public partial class ColorPickerPopup : UserControl
{
    public static readonly DependencyProperty SelectedColorHexProperty = DependencyProperty.Register(
        nameof(SelectedColorHex), typeof(string), typeof(ColorPickerPopup),
        new FrameworkPropertyMetadata("#41D6C3", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorHexChanged));

    public string SelectedColorHex
    {
        get => (string)GetValue(SelectedColorHexProperty);
        set => SetValue(SelectedColorHexProperty, value);
    }

    private static readonly string[] PresetHexes =
    {
        "#41D6C3", "#88C0D0", "#BD93F9", "#FF79C6", "#2F80ED", "#50FA7B",
        "#FFB86C", "#FF5555", "#F2F5F5", "#9AA5A4", "#6E7A79", "#1B1D22"
    };

    private double _h, _s = 1, _v = 1;
    private bool _isUpdatingInternally;
    private bool _draggingSv;
    private bool _draggingHue;

    public ColorPickerPopup()
    {
        InitializeComponent();
        BuildPresets();
        SetFromColor(ThemeService.ParseColorSafe(SelectedColorHex));
        Loaded += (_, _) => SetFromColor(ThemeService.ParseColorSafe(SelectedColorHex));
    }

    private void BuildPresets()
    {
        foreach (var hex in PresetHexes)
        {
            var color = ThemeService.ParseColorSafe(hex);
            var swatch = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 6, 6),
                Background = new SolidColorBrush(color),
                Cursor = Cursors.Hand,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Tag = hex
            };
            swatch.MouseLeftButtonDown += (_, _) =>
            {
                SetFromColor(color);
                PushHexToProperty();
            };
            PresetsControl.Items.Add(swatch);
        }
    }

    private static void OnSelectedColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerPopup picker && !picker._isUpdatingInternally)
            picker.SetFromColor(ThemeService.ParseColorSafe(e.NewValue as string));
    }

    private void SetFromColor(Color color)
    {
        var (h, s, v) = ColorUtils.RgbToHsv(color);
        _h = h; _s = s; _v = v;
        RedrawFromHsv(updateHexBox: true);
    }

    private void RedrawFromHsv(bool updateHexBox)
    {
        var hueColor = ColorUtils.HsvToRgb(_h, 1, 1);
        SvHueLayer.Background = new SolidColorBrush(hueColor);

        var svGrid = (Grid)SvHueLayer.Parent;
        double svWidth = svGrid.ActualWidth > 0 ? svGrid.ActualWidth : 204;
        double svHeight = svGrid.ActualHeight > 0 ? svGrid.ActualHeight : 140;
        Canvas.SetLeft(SvThumb, _s * svWidth);
        Canvas.SetTop(SvThumb, (1 - _v) * svHeight);

        var hueCanvas = (Canvas)HueThumb.Parent;
        var hueGrid = (Grid)hueCanvas.Parent;
        double hueWidth = hueGrid.ActualWidth > 0 ? hueGrid.ActualWidth : 204;
        Canvas.SetLeft(HueThumb, (_h / 360.0) * hueWidth);

        var current = ColorUtils.HsvToRgb(_h, _s, _v);
        LivePreviewSwatch.Background = new SolidColorBrush(current);

        if (updateHexBox)
        {
            var hex = $"#{current.R:X2}{current.G:X2}{current.B:X2}";
            if (HexBox.Text != hex)
                HexBox.Text = hex;
        }
    }

    private void PushHexToProperty()
    {
        var current = ColorUtils.HsvToRgb(_h, _s, _v);
        var hex = $"#{current.R:X2}{current.G:X2}{current.B:X2}";
        _isUpdatingInternally = true;
        SelectedColorHex = hex;
        _isUpdatingInternally = false;
    }

    private void OnSvMouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingSv = true;
        ((IInputElement)sender).CaptureMouse();
        UpdateSvFromMouse((Grid)sender, e.GetPosition((IInputElement)sender));
    }

    private void OnSvMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingSv)
            UpdateSvFromMouse((Grid)sender, e.GetPosition((IInputElement)sender));
    }

    private void OnSvMouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingSv = false;
        ((IInputElement)sender).ReleaseMouseCapture();
    }

    private void UpdateSvFromMouse(Grid grid, Point pos)
    {
        double s = Math.Clamp(pos.X / grid.ActualWidth, 0, 1);
        double v = Math.Clamp(1 - pos.Y / grid.ActualHeight, 0, 1);
        _s = s; _v = v;
        RedrawFromHsv(updateHexBox: true);
        PushHexToProperty();
    }

    private void OnHueMouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = true;
        ((IInputElement)sender).CaptureMouse();
        UpdateHueFromMouse((Grid)sender, e.GetPosition((IInputElement)sender));
    }

    private void OnHueMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingHue)
            UpdateHueFromMouse((Grid)sender, e.GetPosition((IInputElement)sender));
    }

    private void OnHueMouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = false;
        ((IInputElement)sender).ReleaseMouseCapture();
    }

    private void UpdateHueFromMouse(Grid grid, Point pos)
    {
        _h = Math.Clamp(pos.X / grid.ActualWidth, 0, 1) * 360;
        RedrawFromHsv(updateHexBox: true);
        PushHexToProperty();
    }

    private void OnHexBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(HexBox.Text);
            SetFromColor(color);
            PushHexToProperty();
        }
        catch
        {
            // Incomplete/invalid hex while typing — ignore until it parses.
        }
    }
}
