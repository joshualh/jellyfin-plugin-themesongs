using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ThemeSongs.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string ThemeSongUrlTemplate { get; set; } = "";
        // No libraries selected by default; never scan virtual streaming libraries implicitly.
        public List<string> SelectedLibraryIds { get; set; } = new List<string>();
    }
}