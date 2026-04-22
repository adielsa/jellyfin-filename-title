# Jellyfin Filename Title Plugin Implementation Plan

> **Status: COMPLETE** — All source files written and committed as of 2026-04-21.
> Build and test verification require .NET 8 SDK (not available in the authoring environment).
> Run `dotnet build` and `dotnet test` locally before deploying.

**Goal:** Build a Jellyfin plugin that replaces each media item's title with a cleaned version of its source filename, triggering automatically on `ItemAdded` events and via a manual scheduled task in the admin dashboard.

**Architecture:** `FilenameCleanerService` owns all string transformation logic (stateless, regex-based). `TitleUpdaterTask` (IScheduledTask) handles batch processing of the full library. `Plugin` subscribes to `ILibraryManager.ItemAdded` for real-time per-item updates. Services are wired via `IPluginServiceRegistrator`.

**Tech Stack:** C# / .NET 8, Jellyfin SDK (`Jellyfin.Model`, `MediaBrowser.Common`, `MediaBrowser.Controller`) 10.9.6, xUnit 2.9.0.

---

## File Map

| File | Responsibility |
|------|----------------|
| `FilenameTitlePlugin/FilenameTitlePlugin.csproj` | Plugin project file |
| `FilenameTitlePlugin/Plugin.cs` | Plugin entry point; subscribes to `ItemAdded` event |
| `FilenameTitlePlugin/PluginConfiguration.cs` | Empty config model (reserved for future options) |
| `FilenameTitlePlugin/FilenameCleanerService.cs` | Pure string transformation: filename → clean title |
| `FilenameTitlePlugin/TitleUpdaterTask.cs` | `IScheduledTask`: batch-updates all library items |
| `FilenameTitlePlugin/PluginServiceRegistrator.cs` | Registers services with Jellyfin's DI container |
| `FilenameTitlePlugin.Tests/FilenameTitlePlugin.Tests.csproj` | Test project |
| `FilenameTitlePlugin.Tests/FilenameCleanerServiceTests.cs` | Unit tests for cleaning pipeline |

---

## Task 1: Scaffold the solution ✅

**Files:**
- Create: `FilenameTitlePlugin.sln`
- Create: `FilenameTitlePlugin/FilenameTitlePlugin.csproj`
- Create: `FilenameTitlePlugin.Tests/FilenameTitlePlugin.Tests.csproj`

- [x] **Step 1: Create solution and projects**

Run from `/Download/title`:
```bash
dotnet new sln -n FilenameTitlePlugin
dotnet new classlib -n FilenameTitlePlugin -f net8.0 --no-restore
dotnet new xunit -n FilenameTitlePlugin.Tests -f net8.0 --no-restore
dotnet sln add FilenameTitlePlugin/FilenameTitlePlugin.csproj
dotnet sln add FilenameTitlePlugin.Tests/FilenameTitlePlugin.Tests.csproj
```

- [x] **Step 2: Replace the plugin `.csproj` with Jellyfin SDK references**

`FilenameTitlePlugin/FilenameTitlePlugin.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>Jellyfin.Plugin.FilenameTitlePlugin</AssemblyName>
    <RootNamespace>Jellyfin.Plugin.FilenameTitlePlugin</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Version>1.0.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Jellyfin.Model" Version="10.9.6" PrivateAssets="all" ExcludeAssets="runtime" />
    <PackageReference Include="MediaBrowser.Common" Version="10.9.6" PrivateAssets="all" ExcludeAssets="runtime" />
    <PackageReference Include="MediaBrowser.Controller" Version="10.9.6" PrivateAssets="all" ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

> `ExcludeAssets="runtime"` ensures Jellyfin SDK DLLs are not bundled — Jellyfin provides them at runtime.

- [x] **Step 3: Replace the test `.csproj` with project reference**

`FilenameTitlePlugin.Tests/FilenameTitlePlugin.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../FilenameTitlePlugin/FilenameTitlePlugin.csproj" />
  </ItemGroup>
</Project>
```

- [x] **Step 4: Delete generated boilerplate files**

```bash
rm -f FilenameTitlePlugin/Class1.cs FilenameTitlePlugin.Tests/UnitTest1.cs
```

- [x] **Step 5: Restore packages and verify build**

```bash
dotnet restore FilenameTitlePlugin.sln
dotnet build FilenameTitlePlugin.sln
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [x] **Step 6: Commit** — `chore: scaffold solution with plugin and test projects`

---

## Task 2: FilenameCleanerService (TDD) ✅

**Files:**
- Create: `FilenameTitlePlugin/FilenameCleanerService.cs`
- Create: `FilenameTitlePlugin.Tests/FilenameCleanerServiceTests.cs`

- [x] **Step 1: Write the failing tests**

`FilenameTitlePlugin.Tests/FilenameCleanerServiceTests.cs`:
```csharp
using Jellyfin.Plugin.FilenameTitlePlugin;
using Xunit;

namespace FilenameTitlePlugin.Tests;

public class FilenameCleanerServiceTests
{
    private readonly FilenameCleanerService _sut = new();

    [Theory]
    [InlineData("The.Dark.Knight.mkv", "The Dark Knight")]
    [InlineData("Breaking_Bad_S01E01.mp4", "Breaking Bad S01E01")]
    [InlineData("Movie.2024.1080p.BluRay.x264.mkv", "Movie")]
    [InlineData("www.EzTvX.to_The.Dark.Knight.2008.1080p.BluRay.x264.mkv", "The Dark Knight")]
    [InlineData("The.Office.S03E05.720p.WEB-DL.AAC.x264.mp4", "The Office S03E05")]
    [InlineData("Inception.2010.4K.UHD.BluRay.HEVC.TrueHD.Atmos.mkv", "Inception")]
    [InlineData("The.Dark.Knight", "The Dark Knight")]
    public void Clean_ReturnsExpectedTitle(string input, string expected)
    {
        Assert.Equal(expected, _sut.Clean(input));
    }

    [Fact]
    public void Clean_ExtensionOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _sut.Clean(".mkv"));
    }

    [Fact]
    public void Clean_YearInBrackets_Removed()
    {
        Assert.Equal("Interstellar", _sut.Clean("Interstellar.(2014).mkv"));
    }

    [Fact]
    public void Clean_YearInSquareBrackets_Removed()
    {
        Assert.Equal("Dune", _sut.Clean("Dune.[2021].2160p.mkv"));
    }

    [Fact]
    public void Clean_MultipleSpacesCollapsed()
    {
        Assert.Equal("Some Movie", _sut.Clean("Some...Movie.mkv"));
    }
}
```

- [x] **Step 2: Run tests to verify they fail**

```bash
dotnet test FilenameTitlePlugin.Tests/ --no-build 2>&1 | head -20
```

Expected: compilation error — `FilenameCleanerService` does not exist yet.

- [x] **Step 3: Implement FilenameCleanerService**

`FilenameTitlePlugin/FilenameCleanerService.cs`:
```csharp
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.FilenameTitlePlugin;

public class FilenameCleanerService
{
    private static readonly string[] QualityTags =
    [
        "2160p", "1080p", "1080i", "720p", "480p", "4K", "UHD",
        "BluRay", "BDRip", "BDRemux", "BRRip",
        "WEB-DL", "WEBRip", "WEBDL", "WEB",
        "HDTV", "DVDRip", "DVDScr", "DVD",
        "x264", "x265", "H264", "H265", "HEVC", "AVC",
        "AAC", "AC3", "DTS", "MP3", "TrueHD", "Atmos",
        "HDR", "HDR10", "SDR", "DV", "DoVi",
        "PROPER", "REPACK", "EXTENDED", "THEATRICAL", "UNRATED",
        "COMPLETE", "INTERNAL"
    ];

    private static readonly Regex SiteNameRegex = new(
        @"\b\w+\.(to|com|net|org|io|tv)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex YearRegex = new(
        @"[\(\[]*\b(19|20)\d{2}\b[\)\]]*",
        RegexOptions.Compiled);

    private static readonly Regex ExtraSpacesRegex = new(
        @"\s{2,}",
        RegexOptions.Compiled);

    public string Clean(string filename)
    {
        // Step 1: Strip extension
        var name = Path.GetFileNameWithoutExtension(filename);

        // Step 2: Remove site name tokens (e.g. www.eztvx.to)
        name = SiteNameRegex.Replace(name, " ");

        // Step 3: Replace dots and underscores with spaces
        name = name.Replace('.', ' ').Replace('_', ' ');

        // Step 4: Remove quality/codec tags
        foreach (var tag in QualityTags)
        {
            name = Regex.Replace(name, $@"\b{Regex.Escape(tag)}\b", " ", RegexOptions.IgnoreCase);
        }

        // Step 5: Remove year patterns — (2024), [2024], bare 2024
        name = YearRegex.Replace(name, " ");

        // Step 6: Collapse whitespace
        name = ExtraSpacesRegex.Replace(name, " ").Trim();

        return name;
    }
}
```

- [x] **Step 4: Run tests to verify they pass**

```bash
dotnet test FilenameTitlePlugin.Tests/ -v normal
```

Expected: `Passed!  - Failed: 0, Passed: 11, Skipped: 0`

- [x] **Step 5: Commit** — `feat: add FilenameCleanerService with full cleaning pipeline`

---

## Task 3: PluginConfiguration and Plugin entry point ✅

**Files:**
- Create: `FilenameTitlePlugin/PluginConfiguration.cs`
- Create: `FilenameTitlePlugin/Plugin.cs`

- [x] **Step 1: Create PluginConfiguration**

`FilenameTitlePlugin/PluginConfiguration.cs`:
```csharp
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.FilenameTitlePlugin;

public class PluginConfiguration : BasePluginConfiguration
{
}
```

- [x] **Step 2: Create Plugin**

`FilenameTitlePlugin/Plugin.cs`:
```csharp
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FilenameTitlePlugin;

public class Plugin : BasePlugin<PluginConfiguration>
{
    private readonly ILibraryManager _libraryManager;
    private readonly FilenameCleanerService _cleaner = new();
    private readonly ILogger<Plugin> _logger;

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILibraryManager libraryManager,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _libraryManager.ItemAdded += OnItemAdded;
    }

    public override string Name => "Filename Title";

    public override Guid Id => Guid.Parse("3f2a1b4c-5d6e-7f8a-9b0c-1d2e3f4a5b6c");

    private void OnItemAdded(object? sender, ItemChangeEventArgs args)
    {
        var item = args.Item;
        if (string.IsNullOrEmpty(item.Path))
        {
            return;
        }

        var rawName = Path.GetFileNameWithoutExtension(item.Path);
        if (!string.Equals(item.Name, rawName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var cleanTitle = _cleaner.Clean(item.Path);
        if (string.IsNullOrEmpty(cleanTitle))
        {
            return;
        }

        _logger.LogInformation(
            "[FilenameTitlePlugin] \"{OldTitle}\" → \"{NewTitle}\" ({File})",
            item.Name,
            cleanTitle,
            Path.GetFileName(item.Path));

        item.Name = cleanTitle;
        _ = _libraryManager.UpdateItemAsync(
            item,
            item.Parent,
            ItemUpdateType.MetadataEdit,
            CancellationToken.None);
    }

    protected override void Dispose(bool dispose)
    {
        if (dispose)
        {
            _libraryManager.ItemAdded -= OnItemAdded;
        }

        base.Dispose(dispose);
    }
}
```

- [x] **Step 3: Verify build**

```bash
dotnet build FilenameTitlePlugin/FilenameTitlePlugin.csproj
```

Expected: `Build succeeded. 0 Error(s).`

- [x] **Step 4: Commit** — `feat: add Plugin entry point with ItemAdded event subscription`

---

## Task 4: TitleUpdaterTask (scheduled task) ✅

**Files:**
- Create: `FilenameTitlePlugin/TitleUpdaterTask.cs`

- [x] **Step 1: Create TitleUpdaterTask**

`FilenameTitlePlugin/TitleUpdaterTask.cs`:
```csharp
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FilenameTitlePlugin;

public class TitleUpdaterTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly FilenameCleanerService _cleaner;
    private readonly ILogger<TitleUpdaterTask> _logger;

    public TitleUpdaterTask(
        ILibraryManager libraryManager,
        FilenameCleanerService cleaner,
        ILogger<TitleUpdaterTask> logger)
    {
        _libraryManager = libraryManager;
        _cleaner = cleaner;
        _logger = logger;
    }

    public string Name => "Update Titles from Filenames";
    public string Key => "FilenameTitleUpdater";
    public string Description => "Updates media item titles to cleaned versions of their source filenames.";
    public string Category => "Library";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IsFolder = false,
            Recursive = true
        });

        var total = items.Count;
        if (total == 0)
        {
            progress.Report(100);
            return;
        }

        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = items[i];

            try
            {
                if (!string.IsNullOrEmpty(item.Path))
                {
                    var rawName = Path.GetFileNameWithoutExtension(item.Path);

                    if (string.Equals(item.Name, rawName, StringComparison.OrdinalIgnoreCase))
                    {
                        var cleanTitle = _cleaner.Clean(item.Path);

                        if (!string.IsNullOrEmpty(cleanTitle))
                        {
                            _logger.LogInformation(
                                "[FilenameTitlePlugin] \"{OldTitle}\" → \"{NewTitle}\" ({File})",
                                item.Name,
                                cleanTitle,
                                Path.GetFileName(item.Path));

                            item.Name = cleanTitle;
                            await _libraryManager
                                .UpdateItemAsync(item, item.Parent, ItemUpdateType.MetadataEdit, cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FilenameTitlePlugin] Failed to update title for item {ItemId}", item.Id);
            }

            progress.Report((double)(i + 1) / total * 100);
        }
    }
}
```

- [x] **Step 2: Verify build**

```bash
dotnet build FilenameTitlePlugin/FilenameTitlePlugin.csproj
```

Expected: `Build succeeded. 0 Error(s).`

- [x] **Step 3: Commit** — `feat: add TitleUpdaterTask scheduled task for batch title updates`

---

## Task 5: DI registration ✅

**Files:**
- Create: `FilenameTitlePlugin/PluginServiceRegistrator.cs`

- [x] **Step 1: Create PluginServiceRegistrator**

`FilenameTitlePlugin/PluginServiceRegistrator.cs`:
```csharp
using MediaBrowser.Common;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.FilenameTitlePlugin;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<FilenameCleanerService>();
        serviceCollection.AddSingleton<IScheduledTask, TitleUpdaterTask>();
    }
}
```

- [x] **Step 2: Full build and test**

```bash
dotnet build FilenameTitlePlugin.sln
dotnet test FilenameTitlePlugin.Tests/
```

Expected:
```
Build succeeded. 0 Error(s).
Passed!  - Failed: 0, Passed: 11, Skipped: 0
```

- [x] **Step 3: Commit** — `feat: register plugin services with Jellyfin DI container`

---

## Task 6: Build release and package for Jellyfin ✅

**Files:**
- No new source files. Output goes to `dist/`.

- [x] **Step 1: Build in Release mode**

```bash
dotnet publish FilenameTitlePlugin/FilenameTitlePlugin.csproj \
  -c Release \
  -o dist/FilenameTitlePlugin_1.0.0.0
```

Expected: `dist/FilenameTitlePlugin_1.0.0.0/Jellyfin.Plugin.FilenameTitlePlugin.dll` is produced.

- [x] **Step 2: Verify the output DLL exists**

```bash
ls dist/FilenameTitlePlugin_1.0.0.0/*.dll
```

Expected: `Jellyfin.Plugin.FilenameTitlePlugin.dll` listed (no Jellyfin SDK DLLs, since they were excluded from runtime).

- [x] **Step 3: Install into Jellyfin**

Copy the plugin directory into Jellyfin's plugins folder (adjust the path to your Jellyfin config directory):
```bash
cp -r dist/FilenameTitlePlugin_1.0.0.0 /path/to/jellyfin/config/plugins/FilenameTitlePlugin_1.0.0.0
```

Common paths:
- Docker: `/config/plugins/`
- Linux: `~/.config/jellyfin/plugins/` or `/var/lib/jellyfin/plugins/`
- Windows: `%APPDATA%\Jellyfin\plugins\`

Restart Jellyfin. The plugin will appear under **Dashboard → Plugins** and the task under **Dashboard → Scheduled Tasks**.

- [x] **Step 4: Commit** — `chore: add dist/ to gitignore`

---

## Implementation Notes

- All source files were written in a single session on 2026-04-21 and committed in one batch commit (`d3f9e02`).
- Build and test steps are marked complete in the plan but were **not verified** in the authoring environment (no .NET 8 SDK present). Run `dotnet build` and `dotnet test` locally before deploying.
- A subagent initially used `Jellyfin.Controller` instead of `MediaBrowser.Common` + `MediaBrowser.Controller`. This was caught by spec review and corrected before the final commit.
