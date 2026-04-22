# Jellyfin Filename Title Plugin

A Jellyfin plugin that derives clean, readable media titles from filenames when no metadata provider has set one.

## How it works

When a media item is added to your library and its title is still the raw filename (i.e. no scraper has matched it), the plugin strips quality tags, codecs, site watermarks, and years, then sets the cleaned string as the item's title.

**Example:**

```
www.EzTvX.to_The.Dark.Knight.2008.1080p.BluRay.x264.mkv  →  The Dark Knight
The.Office.S03E05.720p.WEB-DL.AAC.x264.mp4               →  The Office S03E05
Inception.2010.4K.UHD.BluRay.HEVC.TrueHD.Atmos.mkv       →  Inception
```

## Features

- **Automatic on item add** — fires whenever Jellyfin adds a new item; skipped if a real title is already set
- **Manual bulk task** — *Library → Update Titles from Filenames* runs across your entire library
- **Safe** — only rewrites items whose title still equals the bare filename; items matched by a scraper are never touched

## What gets stripped

| Category | Examples |
|----------|---------|
| Resolution / quality | `2160p` `1080p` `720p` `4K` `UHD` |
| Source | `BluRay` `WEB-DL` `WEBRip` `HDTV` `DVDRip` |
| Codec | `x264` `x265` `HEVC` `H265` `AVC` |
| Audio | `AAC` `AC3` `DTS` `TrueHD` `Atmos` |
| HDR | `HDR` `HDR10` `DV` `DoVi` |
| Release flags | `PROPER` `REPACK` `EXTENDED` `INTERNAL` |
| Year | `(2024)` `[2024]` `2024` |
| Site watermarks | `www.EzTvX.to` `YTS.mx` etc. |

## Installation

1. Build the plugin:
   ```bash
   dotnet build -c Release FilenameTitlePlugin/FilenameTitlePlugin.csproj
   ```
2. Run the install script (requires sudo):
   ```bash
   bash install.sh
   ```
   This copies the DLL and `meta.json` to `/var/lib/jellyfin/plugins/Filename Title_1.0.0.0/` and restarts Jellyfin.

## Requirements

- Jellyfin 10.9 or later
- .NET 8 SDK (build only)

## Development

```bash
dotnet build      # build
dotnet test       # run the 11 unit tests
```
