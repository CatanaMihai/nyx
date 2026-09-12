---
name: launcher-theming
description: Create or edit visual themes/skins and ambient/open-close animations for the Launcher WPF app (this repo). Use when asked to add a theme preset, change colors/fonts/sizes, add or modify an ambient animation pattern (like Pulse/Shimmer/Glitch), or explain how the launcher's theming system works.
---

# Launcher theming & animation skill

This skill documents the **actual, current architecture** of this repo's theme
and animation system so new presets/properties/animations can be added
consistently, without re-deriving the design from scratch. Read the relevant
section below before touching theme/animation code, and re-open the specific
file before editing it (this doc gives you the map, not a replacement for
reading the code).

## Mental model (read this first)

There are exactly three layers, and changes almost always touch some subset
of all three:

1. **`Themes/ThemeDefinition.cs`** — a plain C# class with every themeable
   property (colors as hex strings, sizes as doubles, a couple of string
   "enum" fields like `AmbientAnimationStyle`). This is also the JSON schema
   for on-disk theme files (`%AppData%\Launcher\Themes\*.json`) — it's
   `System.Text.Json`-serialized directly, no custom converters.
2. **`Themes/ThemeService.cs`** — the *only* code allowed to write into
   `Application.Current.Resources`. `PushToResources(ThemeDefinition t)` is
   the single method that maps every `ThemeDefinition` property to a
   `"Theme*"`-prefixed resource key (brush/thickness/corner-radius/double/
   font-family/etc). Applying a theme = calling `Apply(theme)`, which calls
   `PushToResources` then raises `ThemeChanged`. **Nothing else in the app
   is allowed to touch the resource dictionary.**
3. **XAML consumers** (`Views/MainWindow.xaml`, `Views/SettingsWindow.xaml`,
   `Views/ColorPickerPopup.xaml`) — reference those same keys via
   `DynamicResource`, never hard-coded colors/sizes. `DynamicResource` is
   live: the instant `ThemeService` updates a key, every window using it
   re-renders, with **no restart, no window recreation, no re-indexing**.

A handful of values can't be plain XAML resources (Storyboard durations,
`Window.Width` which needs the glow margin baked in, the ambient animation
loop itself) — those are read directly from `ThemeService.Current` in
`Views/MainWindow.xaml.cs` at the moment they're needed (each open/close,
each `ThemeChanged` event), so they still update live without being
resources.

**Golden rule:** if you're adding a new visual knob, it needs an entry in
*all three* layers (property on `ThemeDefinition` → push in
`ThemeService.PushToResources` → `DynamicResource` reference in XAML, or a
direct `ThemeService.Current.XxxProperty` read in code-behind if it can't be
a resource). If you're adding a new *preset* (skin), you only touch
`Themes/BuiltInThemes.cs`. If you're adding a new *animation pattern*, see
the dedicated section below.

## File map

```
/Themes
    ThemeDefinition.cs   — every themeable property; JSON schema for theme files
    ThemeService.cs      — Apply/PushToResources/Save/Load; owns Application.Resources
    BuiltInThemes.cs      — the shipped presets (Midnight Cyan, Obsidian, Nord,
                            Dracula, Light, Cyberpunk) — ADD NEW PRESETS HERE
    ColorUtils.cs         — HSV<->RGB, used only by the color-picker popup

/Views
    MainWindow.xaml        — the launcher bar itself; every color/size is
                            {DynamicResource ThemeXxx}, nothing hard-coded
    MainWindow.xaml.cs     — AnimateShow/AnimateHide (open/close), and
                            StartAmbientAnimation/StopAmbientAnimation/
                            FireGlitchBurst (the looping "ambient" effects)
    SettingsWindow.xaml(.cs) — the Appearance/Animations tabs that edit a
                            ThemeDefinition live
    ColorPickerPopup.xaml(.cs) — the custom HSV color picker used by every
                            color swatch in Settings

/ViewModels
    SettingsViewModel.cs  — wraps a working ThemeDefinition (`_editing`);
                            every property setter calls ApplyLive() which is
                            `_themeService.Apply(_editing.Clone(), persistName: false)`
```

## How to add a new built-in preset ("skin")

Everything lives in `Themes/BuiltInThemes.cs`. There is no other file to
touch for a plain color-scheme preset:

1. Add a `public const string FooName = "Foo";`.
2. Add a `public static ThemeDefinition Foo() => new() { Name = FooName, ... }`
   method, setting whichever properties differ from `ThemeDefinition`'s
   defaults (you don't have to set every property — anything omitted keeps
   the class's default value).
3. Add `Foo()` to the `All` array.
4. Add `FooName => Foo(),` to the `FindByName` switch.

That's it — `ThemeService.EnsureThemesDirectoryHasBuiltIns()` automatically
seeds `Foo.json` in `%AppData%\Launcher\Themes\` on next startup, and
`SettingsViewModel.AvailableThemes` (built from `BuiltInThemes.All`) picks it
up in the dropdown automatically. No changes needed in `ThemeService`,
`SettingsViewModel`, or any XAML.

**Design guidance for presets:** look at the existing ones for the range —
`MidnightCyan()`/`Obsidian()`/`Nord()`/`Dracula()`/`Light()` are intentionally
restrained (`GlowOpacity` ~0.06–0.24, `GlowRadius` ~14–24). `Cyberpunk()` is
the deliberate exception (`GlowOpacity` 0.42, `GlowRadius` 34, plus an ambient
animation on by default) — if the user asks for something "loud"/"flashy"/
in a specific aesthetic (synthwave, retro terminal, pastel, etc.), it's fine
to push saturation/glow further like Cyberpunk does, but say so in the XML
doc comment the way the others do, so future edits know it's deliberate.

Each preset should set at minimum: `BackgroundColor`, `BorderColor`,
`AccentColor`, `GlowColor`, `GlowOpacity`, `GlowRadius`, `GlowEnabled`,
`SearchTextColor`, `PlaceholderTextColor`, `SearchIconColor`,
`PrimaryTextColor`, `SecondaryTextColor`, `CategoryTextColor`,
`SelectedBackgroundColor`, `SelectedBackgroundOpacity`, `SelectedTextColor`
— that's the full "look" of the bar. Leave `WindowWidth`, `CornerRadius`,
`ResultHeight`, font sizes, etc. at the class defaults unless the request
specifically calls for a different shape/size, not just different colors.

## How to add a brand-new themeable property

Example: adding a `ResultIconCornerRadius` property.

1. **`Themes/ThemeDefinition.cs`**: add the property with a sensible default,
   in the right `// ----- Section -----` group.
2. **`Themes/ThemeService.cs`** `PushToResources`: add
   `res["ThemeResultIconCornerRadius"] = new CornerRadius(t.ResultIconCornerRadius);`
   (or `SetBrush`/`SetColor` for colors — see the helpers already there).
   Key naming convention: **always prefixed `Theme`**, then PascalCase,
   matching the `ThemeDefinition` property name as closely as possible
   (`ResultIconCornerRadius` → `ThemeResultIconCornerRadius`).
3. **`Views/MainWindow.xaml`**: reference it via
   `CornerRadius="{DynamicResource ThemeResultIconCornerRadius}"` on whatever
   element needs it. Never hard-code the value.
4. If it should be user-editable in Settings: add a property + backing field
   to `ViewModels/SettingsViewModel.cs` following the existing pattern —
   ```csharp
   public double ResultIconCornerRadius
   {
       get => _editing.ResultIconCornerRadius;
       set { _editing.ResultIconCornerRadius = Clamp(value, 0, 20); ApplyLive(); OnPropertyChanged(); }
   }
   ```
   then add it to `RaiseAllThemePropertiesChanged()`'s list (so switching
   presets in the dropdown refreshes the Settings UI for it too), and add a
   `TextBlock`+`Slider` (styled with `{StaticResource ThemedSlider}`) in the
   relevant `Views/SettingsWindow.xaml` section, inside a `Border
   Style="{StaticResource SectionCard}"`.

`ApplyLive()` is `_themeService.Apply(_editing.Clone(), persistName: false)`
— every single Settings-tab edit calls this, which is why every edit is
instantly live in both the Settings preview panel and the real MainWindow
without an explicit "preview" mechanism: they all read the exact same
`DynamicResource` keys.

## How the ambient animation system works (and how to add a new pattern)

"Ambient" animations are the looping effects played *while the bar is open*
(distinct from the one-shot open/close animation). They live entirely in
`Views/MainWindow.xaml.cs`:

- `ThemeDefinition.AmbientAnimationStyle` (string: `"None"`, `"Pulse"`,
  `"Shimmer"`, `"Glitch"`) and `AmbientAnimationSpeedSeconds` (double) are the
  only theme-level knobs.
- `MainWindow.StartAmbientAnimation()` is called from `AnimateShow()` (after
  the open animation starts) and from the `ThemeService.ThemeChanged` handler
  (if the bar is currently visible) — so switching patterns live in Settings
  restarts the loop instantly.
- `MainWindow.StopAmbientAnimation()` is called from `AnimateHide()` and at
  the top of `StartAmbientAnimation()` (so restarting cleanly stops whatever
  was running first). It must **null out every animation it might have
  started** (`BeginAnimation(prop, null)` reverts to the DynamicResource-driven
  base value — no extra bookkeeping needed) and reset any manually-touched
  property (e.g. `ShimmerOverlay.Opacity = 0`).
- Existing patterns, as a reference for style:
  - **Pulse**: a single `DoubleAnimation` on `GlowEffect.Opacity`,
    `AutoReverse = true`, `RepeatBehavior.Forever` — a smooth breathing glow.
  - **Shimmer**: a `DoubleAnimation` on `ShimmerTransform.X` (a
    `TranslateTransform` on the dedicated `ShimmerOverlay` element already in
    `MainWindow.xaml`, a soft gradient Border spanning all rows) sweeping
    from off-screen-left to off-screen-right, `RepeatBehavior.Forever`.
  - **Glitch**: NOT a smooth loop — a `DispatcherTimer`
    (`_glitchTimer`) that reschedules itself with a *randomized* interval
    after every tick (`NextGlitchInterval`), and each tick calls
    `FireGlitchBurst(theme)` which fires a short (~100-150ms), discrete
    (non-eased) burst: a couple of `DiscreteDoubleKeyFrame`s jittering
    `TranslateTransform.X` by a few pixels, a `DiscreteColorKeyFrame` flash of
    `GlowEffect.Color` toward the accent color, and a brief opacity dip on
    `RootBorder`. For "Glitch", `AmbientAnimationSpeedSeconds` means the
    *average seconds between bursts*, not a cycle duration — document that
    distinction if you add another burst-style (non-looping) pattern too.

**To add a new pattern** (e.g. "Scanline", "Flicker", whatever the user asks
for):

1. Pick a name string (e.g. `"Scanline"`) — this is what goes in
   `ThemeDefinition.AmbientAnimationStyle`.
2. Add a `case "Scanline":` block in `MainWindow.StartAmbientAnimation()`'s
   switch. Decide: is it a smooth continuous loop (`DoubleAnimation` +
   `RepeatBehavior.Forever`, like Pulse/Shimmer) or an irregular/discrete
   burst pattern (`DispatcherTimer` + keyframe burst, like Glitch)? Match
   whichever existing pattern is structurally closest to what you're building.
3. Make sure `StopAmbientAnimation()` clears anything the new case might
   start (new animated properties need their own `BeginAnimation(prop, null)`
   line; a new `DispatcherTimer` needs its own stop+null).
4. If it needs a new visual element (like `ShimmerOverlay` for Shimmer), add
   it inside `RootBorder`'s content `Grid` in `MainWindow.xaml`
   (`Grid.RowSpan="3"`, `IsHitTestVisible="False"`, `Opacity="0"` by default)
   so it's clipped correctly — see "Clipping gotcha" below.
5. Add `"Scanline"` to `SettingsViewModel.AvailableAmbientStyles` so it shows
   up in the Settings dropdown.
6. If a preset should use it by default, set
   `AmbientAnimationStyle = "Scanline"` in that preset's method in
   `BuiltInThemes.cs`.

### Clipping gotcha (important — read before adding visual elements)

`RootBorder` (the rounded bar itself) does **not** reliably auto-clip a
child whose `RenderTransform` moves it beyond the border's own bounds (this
caused a real bug where the Shimmer sweep — and, separately, tall result
lists — briefly rendered outside the rounded corners). The fix already in
place is `MainWindow.UpdateRootClip()`, which sets an explicit
`RectangleGeometry` clip on `RootBorder` sized to its `ActualWidth`/
`ActualHeight` with the theme's corner radius, refreshed on every
`SizeChanged` and theme change. **Any new ambient effect that moves an
element via `RenderTransform` is automatically covered by this clip as long
as the element is a descendant of `RootBorder`** — don't disable or bypass
`UpdateRootClip`, and don't add new content outside `RootBorder`.

### Animation properties already "borrowed" — don't collide with them

- `TranslateTransform.Y` — used by the open/close animation (vertical slide).
- `TranslateTransform.X` — otherwise unused by open/close, so Glitch reuses
  it for horizontal jitter. Safe to reuse for another X-axis effect too,
  but don't run two ambient patterns' X-animations simultaneously (the
  switch in `StartAmbientAnimation` already only runs one case per call).
- `ScaleTransform.ScaleX`/`ScaleY` — used by the open/close animation.
- `GlowEffect.Opacity` — used by Pulse. `GlowEffect.Color` — used by Glitch.
- `RootBorder.Opacity` (the `Window.Opacity` inherited property is separate
  and used by open/close fade) — used by Glitch's flicker dip.

If a new pattern needs one of these, either reuse it carefully (matching how
Glitch reuses `TranslateTransform.X`) or add a new dedicated element/property
instead of colliding with the open/close animation's own use of Opacity/Y/Scale.

## Editing the color picker

`Views/ColorPickerPopup.xaml(.cs)` is a small dependency-free HSV picker (SV
square + hue strip + hex box + presets), exposed as a single
`SelectedColorHex` (string) dependency property. `SettingsWindow` hosts one
shared instance in a `Popup`, retargeted per-swatch-click via
`SettingsViewModel.BeginEditColor(fieldName)` / `ActiveColorHex` (a proxy
property that reads/writes whichever of the six color fields is "active").
To add a color swatch for a new property, follow the existing six in
`SettingsWindow.xaml` (`Button Style="{StaticResource SwatchButton}"` with
`Tag="YourPropertyName"`, `Click="OnSwatchClick"`) — `OnSwatchClick` in
`SettingsWindow.xaml.cs` already handles any tag generically via
`BeginEditColor`, so wiring a new one is just adding the matching case in
`SettingsViewModel.ActiveColorHex`'s get/set switch plus the XAML button/hexbox.

## Build & smoke-test workflow

```bash
dotnet build -c Debug    # iterate
dotnet build -c Release  # final check before calling it done
```

Gotchas specific to this project (already worked around everywhere they
occur, but relevant if you add new code files):

- `UseWindowsForms=true` is set in `Launcher.csproj` (for the tray icon), so
  `System.Windows.Forms` and `System.Windows`/`System.Windows.Media`/
  `System.Windows.Input` types collide (`Button`, `Color`, `Cursors`,
  `ColorConverter`, `KeyEventArgs`, `MouseButtonEventArgs`, `MouseEventArgs`,
  `Point`, `Application`, `UserControl`, etc.). New `.cs` files under
  `Views/`/`ViewModels/` that use any of these need explicit
  `using Foo = System.Windows.Whatever.Foo;` aliases at the top — copy the
  pattern from the top of `Views/SettingsWindow.xaml.cs` or
  `Views/ColorPickerPopup.xaml.cs`.
- If `dotnet build` fails with `MSB3027`/file-locked errors, a previous
  `Launcher.exe` is still running — `Stop-Process -Name Launcher -Force`
  first (PowerShell) before rebuilding.
- To smoke-test a theme/animation change without touching the running
  desktop's other apps: write
  `%AppData%\Launcher\settings.json` directly with
  `"CurrentThemeName": "YourPreset"` and start the exe — this loads and
  applies the theme with **zero keyboard/mouse input needed**, so it's the
  safest way to verify a preset applies and the app doesn't crash. Only send
  synthetic `Ctrl+Space`/mouse input if you actually need to see it rendered
  (screenshot), and be aware those key/mouse events go to whatever the OS
  foreground window actually is on a shared/real desktop, not necessarily
  the launcher — verify with `GetForegroundWindow`/`GetWindowThreadProcessId`
  or just check `%AppData%\Launcher\launcher.log` and the process exit code
  first before assuming a synthetic key reached the app.
