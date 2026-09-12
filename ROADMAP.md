# Roadmap

Nyx ships a fuzzy app launcher and a full visual theming system today (see
[THEMING.md](THEMING.md)) — a `ThemeService`, built-in presets, a
live-editable Custom theme, and a Settings window with a live preview.
Everything below is future work, kept deliberately out of scope for now so
the core stays focused and solid.

The architecture is already set up for this: each item below is a new
`ISearchProvider` implementation registered in `App.xaml.cs`
(`searchEngine.RegisterProvider(new ...Provider(...))`) plus, where useful, its
own `Services/` class for the non-search side effects (executing a command,
switching an audio device, etc.). No changes to `SearchEngine`, `MainViewModel`,
or the window/animation code should be required to add any of these.

## Ideas / candidates

- **File search** — a provider indexing (or querying, via the Windows Search
  index / `IFileSearch`) file system paths, with its own ranking tuned for
  filenames vs. the app-name ranking used today.
- **Calculator** — detect an arithmetic-looking query (e.g. `12 * (4 + 2)`)
  and show a live-evaluated result as the top hit; Enter copies the result.
- **Web search** — a fallback provider (lowest priority) that turns any
  unmatched query into a "Search the web for '...'" result opening the
  default browser.
- **Commands** — a small registry of built-in actions ("Lock PC", "Sleep",
  "Restart", "Empty Recycle Bin", "Open Settings > ...") matched by name like
  an app would be.
- **Volume control** — inline volume up/down/mute as a provider triggered by
  keywords, using `NAudio` or the Core Audio (`IAudioEndpointVolume`) APIs.
- **Audio device switching** — list playback/recording devices as results;
  selecting one sets it as the default via `IPolicyConfig`.
- **Clipboard history** — a background clipboard listener persisting recent
  entries (text initially), searchable and re-insertable via Enter.
- **Timers / reminders** — parse "timer 5m" style queries, run a background
  timer, and show a toast/notification on expiry.

## Also planned (not provider-specific)

- Live global-shortcut rebinding from the Settings General tab (currently
  requires editing `settings.json` and restarting — see README limitations).
- A "Start with Windows" toggle in Settings that wires up the Registry Run
  key described in the README.
- Theme import/export UI (drag a `.json` file onto Settings, or "Export
  current theme..."), and support for multiple saved custom themes rather
  than a single "Custom" slot.
- Result ranking that blends provider identity (e.g. always show at most N
  file results above app results) once more than one provider is active.
- Optional lightweight update mechanism.
