# Theming

Nyx's entire visual appearance — colors, sizes, glow, fonts, animation
timing — is data-driven. Nothing about how it looks is hard-coded in XAML;
everything is resolved at runtime from a `ThemeDefinition` and pushed into
WPF's resource dictionary, so changing a theme is instant and never requires
restarting the app.

## Architecture

```
/Themes
    ThemeDefinition.cs   — plain data model: every themeable property (also the on-disk JSON schema)
    ThemeService.cs      — loads/applies/saves themes; the only place that touches Application.Resources
    BuiltInThemes.cs     — the shipped presets (Midnight Cyan, Obsidian, Nord, Dracula, Light, Cyberpunk)
```

**Flow:**

1. At startup, `App.xaml.cs` creates a `ThemeService` and calls
   `Initialize(settings.CurrentThemeName)`. This resolves the theme (a
   built-in preset, a file in the Themes folder, or a fallback) and applies
   it — before `MainWindow` is constructed, so the very first frame is
   already themed correctly.
2. `ThemeService.Apply(theme)` writes every visual property into
   `Application.Current.Resources` under stable keys like
   `ThemeWindowBackgroundBrush`, `ThemeAccentBrush`, `ThemeGlowRadius`, etc.
3. `MainWindow.xaml` (and `SettingsWindow.xaml`'s live preview panel)
   reference those same keys via `DynamicResource`. WPF's `DynamicResource`
   is inherently live: the moment a key's value changes in the resource
   dictionary, every element using it re-renders — no manual "refresh" or
   window recreation needed.
4. A handful of values aren't expressible as static resources (the
   Storyboard durations used when opening/closing, and `Window.Width`, which
   also needs the glow's margin baked in) — those are read directly from
   `ThemeService.Current` at the moment they're needed (each open/close,
   each width recalculation), so they still pick up a live theme change
   immediately, just via code instead of XAML.

Applying a theme **only** touches the resource dictionary. It never touches
`AppDiscoveryService`, `SearchEngine`, or any other functional service — so
switching themes, even rapidly while dragging a slider in Settings, causes
no re-indexing, no disk access, and no dropped search state.

## Themeable properties

All properties live on `ThemeDefinition` (`/Themes/ThemeDefinition.cs`):

| Group | Properties |
|---|---|
| Window | `BackgroundColor`, `BackgroundOpacity`, `BorderColor`, `BorderOpacity`, `BorderThickness`, `CornerRadius`, `WindowWidth`, `WindowMinHeight`, `InnerPadding` |
| Accent / glow | `AccentColor`, `GlowColor`, `GlowOpacity`, `GlowRadius`, `GlowEnabled` |
| Search box | `SearchTextColor`, `PlaceholderTextColor`, `SearchFontFamily`, `SearchFontSize`, `SearchIconColor`, `SearchBoxHeight` |
| Results | `PrimaryTextColor`, `SecondaryTextColor`, `CategoryTextColor`, `SelectedBackgroundColor`, `SelectedBackgroundOpacity`, `SelectedTextColor`, `ResultCornerRadius`, `ResultHeight`, `ResultSpacing`, `IconSize`, `ResultTitleFontSize`, `ResultSecondaryFontSize` |
| Layout | `MaxVisibleResults`, `ResultAreaSpacing`, `HorizontalPadding`, `VerticalPadding` |
| Animation | `OpenDurationMs`, `CloseDurationMs`, `ResultSelectionDurationMs`, `OpenScaleFrom`, `OpenYOffset`, `AnimationsEnabled` |

Colors are hex strings (`"#RRGGBB"`); every color is parsed through
`ThemeService.ParseColorSafe`, which never throws — an invalid or missing
hex value falls back to a neutral gray and logs a warning instead of
crashing the app or corrupting the rest of the theme.

Note: `AppSettings.MaxResultCount` (in `settings.json`) is a *separate*,
functional cap on how many results `SearchEngine` computes; `ThemeDefinition.MaxVisibleResults`
only controls how many rows are visible before the result list scrolls.

## Built-in presets

Shipped in `/Themes/BuiltInThemes.cs` — tasteful, curated looks (names are
purely descriptive; no third-party branding or assets):

- **Midnight Cyan** — the launcher's original look: near-black, a subtle cyan accent and glow, white text.
- **Obsidian** — almost black with white/grey UI and essentially no glow; extremely minimal.
- **Nord** — dark navy/blue-grey with a pale blue accent and soft, muted text.
- **Dracula** — dark purple-grey with a purple/pink accent and a slightly stronger selected-result highlight.
- **Light** — translucent off-white background, dark text, a subtle neutral shadow, blue accent, no aggressive glow.
- **Cyberpunk** — deep near-black background with a hot cyan/magenta neon clash, a strong glow, and the ambient **Glitch** pattern enabled by default (irregular signal-jitter bursts, not a smooth sweep) — the one deliberately flashy preset, in contrast to the restraint of the others.
- **Custom** — whatever the user has edited and saved in Settings; not a fixed preset.

## Settings UI

Open Settings with **Ctrl+,** or from the tray icon's context menu. It's a
custom-chrome window (draggable title bar, its own close button) styled
consistently with the launcher itself — dark cards, themed sliders/combo
boxes/checkboxes, not default system controls. It has a sidebar with icons
(General / Appearance / Search / Animations / About); Appearance is where
theme editing happens:

- A **Theme** dropdown to jump straight to a preset (or "Custom").
- **Colors**: click a swatch to open a compact custom color picker — a
  saturation/value square, a hue strip, a row of preset swatches, and a hex
  field for typing an exact value if you prefer (see `Views/ColorPickerPopup.xaml`,
  a small dependency-free control — no third-party package). Whichever way
  you set it, invalid hex typed directly is safely ignored (reverts to the
  last valid value) rather than crashing the app or corrupting the theme.
- **Window**: opacity, corner radius, width, border thickness sliders.
- **Glow**: an enable checkbox plus intensity/radius sliders.
- **Results**: result row height, icon size, spacing sliders.
- A **live preview** panel at the top of Appearance that mimics the real
  launcher (search box + three sample results) using the *exact same*
  `DynamicResource` keys as `MainWindow.xaml` — it isn't a separate
  approximation, so what you see there is exactly what the real launcher
  looks like.

### Rebinding the global shortcuts

The General tab shows both hotkeys — "Open / close launcher" (default
Ctrl+Space) and "Open settings" (default Ctrl+,) — as click-to-record boxes.
Click one (or Tab to it) and press the new key combination; it's re-registered
immediately via `HotkeyService.Rebind` and saved to `settings.json` — no
restart, and no separate "Apply" step. If the combination is already owned by
another running application, the box briefly shows "Already in use" and keeps
the previous binding.

### Ambient animation (Animations tab)

Beyond the one-shot open/close animation, the bar can play a subtle **looping**
effect for as long as it's open:

- **None** (default) — nothing extra, stays minimal.
- **Pulse** — the glow gently "breathes" (opacity oscillates) at a configurable
  cycle speed.
- **Shimmer** — a soft diagonal highlight sweeps across the bar in a loop.
- **Glitch** — irregular short signal-jitter bursts: a couple of sharp
  horizontal jumps, a quick flash of the glow color toward the accent color,
  and a brief opacity flicker, firing at randomized intervals rather than a
  smooth loop. This is the pattern that actually reads as "cyberpunk" rather
  than just "shiny," and is the **Cyberpunk** preset's default.

Pulse and Shimmer are ordinary `DoubleAnimation`s with `RepeatBehavior.Forever`.
Glitch instead uses a `DispatcherTimer` (`MainWindow.FireGlitchBurst`) that
reschedules itself with a new randomized interval after every burst — for
Glitch, `AmbientAnimationSpeedSeconds` means the *average* gap between bursts,
not a cycle duration, so it never feels metronomic. All three are
started/stopped by `MainWindow.AnimateShow`/`AnimateHide` (see
`ThemeDefinition.AmbientAnimationStyle`/`AmbientAnimationSpeedSeconds`) —
purely decorative, none of them ever touch search/launch state, and switching
patterns live (even while the bar is open, via a Settings edit) restarts the
loop instantly through the same `ThemeChanged` event everything else reacts to.

Every edit applies immediately (to both the preview and the real launcher,
if it's open) via `ThemeService.Apply(..., persistName: false)` — nothing
is written to disk until you explicitly act:

- **Save as Custom theme** writes the current edits to `Custom.json` and
  sets it as the active theme in `settings.json`.
- **Reset to Midnight Cyan** discards edits and reapplies that preset.
- Closing Settings **without** saving discards any live-preview edits and
  restores whatever theme was last actually saved/active — `settings.json`
  and the theme files on disk are never left in a half-edited state.

Settings is fully keyboard-operable: Tab/Shift+Tab move between controls,
arrow keys adjust the focused slider/dropdown, Enter activates a button,
Esc closes the window, and Ctrl+S saves (equivalent to clicking "Save as
Custom theme").

## Custom theme files

Themes are plain JSON, stored at:

```
%AppData%\Nyx\Themes\
    Midnight Cyan.json
    Obsidian.json
    Nord.json
    Dracula.json
    Light.json
    Cyberpunk.json
    Custom.json          (created the first time you click "Save")
```

The built-in presets are written out here automatically the first time the
app runs (if not already present), so they're always available for
hand-editing or copying as a starting point for your own theme. You don't
need to edit them by hand, but the architecture supports it: any well-formed
`ThemeDefinition` JSON file dropped into that folder is
picked up (matched to the launcher by its `"Name"` field) the next time
Settings' theme dropdown is opened.

Example (`My Purple Theme.json`):

```json
{
  "Name": "My Purple Theme",
  "BackgroundColor": "#111016",
  "BackgroundOpacity": 0.92,
  "AccentColor": "#A855F7",
  "BorderColor": "#A855F7",
  "CornerRadius": 20,
  "GlowEnabled": true,
  "GlowColor": "#A855F7",
  "GlowOpacity": 0.25,
  "GlowRadius": 18
}
```

(Fields left out simply keep `ThemeDefinition`'s defaults when deserialized.)

## Safety / fallback

If `settings.json` or a theme file is missing, malformed, or contains an
invalid color:

- The bad file/value is logged (`%AppData%\Nyx\nyx.log`) and ignored.
- `ThemeService` falls back to the compiled-in **Midnight Cyan** definition
  — never to a half-applied or null theme.
- The launcher always starts. A corrupt theme file can never prevent the
  app from opening.

## Known limitations

- The built-in presets are seeded as files on first run but are not
  currently protected from being overwritten if you save a custom theme
  using the exact same name as a built-in (e.g. naming a custom theme
  "Nord.json" some other way) — in practice this only happens if you hand-edit
  the Themes folder, since Save always writes to `Custom.json`.
- There's no in-app theme *import* button yet (e.g. "Load theme from file...")
  — a file simply needs to be placed in the Themes folder and will be
  picked up next time the dropdown opens. A dedicated import/export UI is a
  natural next step.
- The color picker's saturation/value square and hue strip are custom-drawn
  (a couple of gradient `Border`s + mouse-drag math) rather than a hardware-
  accelerated shader — perfectly smooth at this size, but not something to
  scale up into a large standalone picker window.
- Only one "Custom" slot exists; saving again overwrites the previous custom
  theme rather than letting you keep multiple named custom themes side by
  side (you can work around this by renaming `Custom.json` yourself between
  saves).
- `MaxVisibleResults` controls the scrollable viewport height, but very
  extreme `ResultHeight`/`IconSize` combinations aren't clamped against each
  other, so an unusual combination (e.g. a tiny `ResultHeight` with a huge
  `IconSize`) can visually overlap — the Settings sliders are range-limited
  to keep this unlikely, but it isn't cross-validated.
