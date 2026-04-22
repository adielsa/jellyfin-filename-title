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

### Requirements

- Jellyfin 10.9 or later (running on Linux)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- `sudo` access on the machine running Jellyfin

### One-shot copy-paste install

```bash
git clone https://github.com/adielsa/jellyfin-filename-title.git
cd jellyfin-filename-title
dotnet build -c Release FilenameTitlePlugin/FilenameTitlePlugin.csproj

PLUGIN_DIR="/var/lib/jellyfin/plugins/Filename Title_1.0.0.0"
sudo mkdir -p "$PLUGIN_DIR"
sudo cp FilenameTitlePlugin/bin/Release/net8.0/Jellyfin.Plugin.FilenameTitlePlugin.dll "$PLUGIN_DIR/"
sudo tee "$PLUGIN_DIR/meta.json" > /dev/null << 'EOF'
{
  "category": "Metadata",
  "changelog": "Initial release",
  "description": "Sets item titles from cleaned-up filenames when no metadata provider has set a title",
  "guid": "3f2a1b4c-5d6e-7f8a-9b0c-1d2e3f4a5b6c",
  "name": "Filename Title",
  "overview": "Derive item titles from filenames",
  "owner": "local",
  "targetAbi": "10.9.0.0",
  "timestamp": "2026-04-22T00:00:00.0000000Z",
  "version": "1.0.0.0",
  "status": "Active",
  "autoUpdate": false,
  "assemblies": []
}
EOF
sudo chown -R jellyfin:jellyfin "$PLUGIN_DIR"
sudo systemctl restart jellyfin
```

After restarting, the plugin appears under **Dashboard → Plugins** as *Filename Title*.

### Verify it loaded

```bash
sudo journalctl -u jellyfin -n 50 | grep -i "filename\|plugin"
```

## Development

```bash
dotnet build      # build
dotnet test       # run the 11 unit tests
```
