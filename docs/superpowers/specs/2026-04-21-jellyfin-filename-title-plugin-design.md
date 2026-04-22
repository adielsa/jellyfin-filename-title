# Jellyfin Filename Title Plugin — Design Spec

**Date:** 2026-04-21
**Status:** Implemented

## Overview

A Jellyfin plugin that replaces each media item's title with a cleaned version of its source filename. Applies to all library types. Triggers automatically when new items are added during a library scan, and is also available as a manual scheduled task in the admin dashboard.

## Architecture

```
FilenameTitlePlugin/
├── Plugin.cs                    # Entry point, registers plugin and event subscription
├── PluginConfiguration.cs       # Config model (reserved for future settings)
├── FilenameCleanerService.cs    # Pure function: raw filename → clean title
├── TitleUpdaterTask.cs          # IScheduledTask implementation
├── PluginServiceRegistrator.cs  # Registers services with Jellyfin's DI container
└── FilenameTitlePlugin.csproj
```

**Target framework:** `net8.0`
**Jellyfin SDK packages:**
- `Jellyfin.Model` 10.9.6
- `MediaBrowser.Common` 10.9.6
- `MediaBrowser.Controller` 10.9.6

All three are referenced with `ExcludeAssets="runtime"` so Jellyfin's own copies are used at runtime.

### Components

- **`Plugin.cs`** — Inherits `BasePlugin<PluginConfiguration>`. Holds plugin metadata (name, GUID, version). In the constructor, subscribes to `ILibraryManager.ItemAdded` to auto-trigger title cleaning on newly scanned items.
- **`PluginConfiguration.cs`** — Empty configuration model; reserved for future options (e.g., per-library enable/disable, custom tag blocklist).
- **`FilenameCleanerService`** — Stateless service. Single public method: `string Clean(string filename)`. All regex logic lives here. Regexes are `static readonly` and `Compiled` for performance.
- **`TitleUpdaterTask`** — Implements `IScheduledTask`. Fetches all items from all libraries via `ILibraryManager`, runs each through `FilenameCleanerService`, saves changes. Reports item-level progress to Jellyfin's task progress API.
- **`PluginServiceRegistrator`** — Implements `IPluginServiceRegistrator`. Registers `FilenameCleanerService` and `TitleUpdaterTask` with Jellyfin's DI container so they are discoverable at runtime.

## Filename Cleaning Pipeline

Steps applied in order to the raw filename:

1. **Strip file extension** — `Path.GetFileNameWithoutExtension()` removes `.mkv`, `.mp4`, `.avi`, etc.
2. **Remove site name tokens** — regex `\b\w+\.(to|com|net|org|io|tv)\b` strips `www.eztvx.to` and similar patterns (case-insensitive)
3. **Replace dots and underscores with spaces** — `My.Movie.2024` → `My Movie 2024`
4. **Remove quality/codec tags** — word-boundary match against a known-token list (case-insensitive):
   `2160p`, `1080p`, `1080i`, `720p`, `480p`, `4K`, `UHD`, `BluRay`, `BDRip`, `BDRemux`, `BRRip`, `WEB-DL`, `WEBRip`, `WEBDL`, `WEB`, `HDTV`, `DVDRip`, `DVDScr`, `DVD`, `x264`, `x265`, `H264`, `H265`, `HEVC`, `AVC`, `AAC`, `AC3`, `DTS`, `MP3`, `TrueHD`, `Atmos`, `HDR`, `HDR10`, `SDR`, `DV`, `DoVi`, `PROPER`, `REPACK`, `EXTENDED`, `THEATRICAL`, `UNRATED`, `COMPLETE`, `INTERNAL`
5. **Remove year patterns** — regex `[\(\[]*\b(19|20)\d{2}\b[\)\]]*` strips `(2024)`, `[2024]`, and bare `2024`
6. **Collapse whitespace** — trim and collapse consecutive spaces to one

**Example:**
```
www.EzTvX.to_The.Dark.Knight.2008.1080p.BluRay.x264.mkv
→ The Dark Knight
```

## Triggering Behavior

### Auto (on item add)
- `Plugin.cs` subscribes to `ILibraryManager.ItemAdded` at startup.
- On each new item, the plugin applies the safety rule (see below) then cleans and updates the title.
- Event handler unsubscribed in `Dispose()` to prevent memory leaks.

### Manual (scheduled task)
- Visible in Jellyfin admin panel under **Scheduled Tasks** as **"Update Titles from Filenames"**.
- Default schedule: never (manual trigger via "Run Now").
- Reports progress as `(i + 1) / total * 100` percent for the admin UI progress bar.
- Logs each change: `[FilenameTitlePlugin] "Old Title" → "New Title" (filename.mkv)`

## Safety Rule

The plugin only overwrites a title if the item's **current title equals `Path.GetFileNameWithoutExtension(item.Path)`** — i.e., Jellyfin left the name as the raw filename because no metadata provider found a match. This prevents the plugin from overwriting correctly identified titles from TMDb, TVDb, or other providers.

## Error Handling

- If `Clean()` returns an empty string after all transformations, the title is left unchanged.
- In `TitleUpdaterTask`, each item update is wrapped in a try/catch — exceptions are logged as warnings and the task continues to the next item without aborting.
- The auto-trigger event handler uses fire-and-forget (`_ = UpdateItemAsync(...)`) since synchronous waiting in an event handler is not appropriate.

## Testing

- **Unit tests** (`FilenameCleanerServiceTests`): 11 tests covering extension stripping, dot/underscore replacement, quality tag removal, year pattern removal (bare, parenthesised, bracketed), site name removal, whitespace collapsing, and combined real-world filenames.
- **Integration**: mount a test library in Jellyfin, run the scheduled task, verify titles update correctly. Requires a running Jellyfin instance.

## Build & Installation

Requires **.NET 8 SDK** to build.

```bash
# Build
dotnet publish FilenameTitlePlugin/FilenameTitlePlugin.csproj -c Release -o dist/FilenameTitlePlugin_1.0.0.0

# Install (adjust to your Jellyfin config path)
cp -r dist/FilenameTitlePlugin_1.0.0.0 /path/to/jellyfin/config/plugins/FilenameTitlePlugin_1.0.0.0
```

Common plugin paths:
- Docker: `/config/plugins/`
- Linux: `~/.config/jellyfin/plugins/` or `/var/lib/jellyfin/plugins/`
- Windows: `%APPDATA%\Jellyfin\plugins\`

Restart Jellyfin after installing. The plugin appears under **Dashboard → Plugins** and the task under **Dashboard → Scheduled Tasks**.
