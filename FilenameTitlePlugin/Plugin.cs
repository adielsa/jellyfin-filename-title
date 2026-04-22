using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FilenameTitlePlugin;

public class Plugin : BasePlugin<PluginConfiguration>, IDisposable
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

        // Safety rule: only update if the current title is still the raw filename
        // (i.e., no metadata provider has set a real title)
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
            item.GetParent(),
            ItemUpdateType.MetadataEdit,
            CancellationToken.None);
    }

    public void Dispose()
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        GC.SuppressFinalize(this);
    }
}
