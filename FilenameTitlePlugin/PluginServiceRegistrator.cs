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
