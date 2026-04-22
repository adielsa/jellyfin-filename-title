#!/usr/bin/env bash
set -e

PLUGIN_DIR="/var/lib/jellyfin/plugins/Filename Title_1.0.0.0"
DLL="/Download/title/FilenameTitlePlugin/bin/Release/net8.0/Jellyfin.Plugin.FilenameTitlePlugin.dll"

sudo mkdir -p "$PLUGIN_DIR"
sudo cp "$DLL" "$PLUGIN_DIR/"
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
sudo journalctl -u jellyfin -n 30 | grep -i "plugin\|filename\|error"
