using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;

namespace Jellyfin.Plugin.ThemeSongs
{
    public class ThemeSongsManager : IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<ThemeSongsManager> _logger;

        public ThemeSongsManager(ILibraryManager libraryManager, ILogger<ThemeSongsManager> logger)
        {
            _libraryManager = libraryManager;
            _logger = logger;
        }

        private IEnumerable<Series> GetSelectedSeries()
        {
            var selectedIds = Plugin.Instance?.Configuration?.SelectedLibraryIds;
            if (selectedIds == null || selectedIds.Count == 0)
            {
                _logger.LogInformation("No libraries selected. Theme song scan skipped.");
                yield break;
            }

            var seen = new HashSet<Guid>();
            foreach (var idText in selectedIds)
            {
                if (!Guid.TryParse(idText, out var libraryId))
                {
                    _logger.LogWarning("Invalid selected Jellyfin library ID: {libraryId}", idText);
                    continue;
                }

                _logger.LogInformation("Scanning theme songs in selected library {libraryId}", libraryId);
                var series = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    ParentId = libraryId,
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true
                }).OfType<Series>();

                foreach (var item in series)
                {
                    if (seen.Add(item.Id))
                    {
                        yield return item;
                    }
                }
            }
        }

        private bool TryBuildThemeSongUrl(Series series, string template, out string link)
        {
            var result = template;
            var replacements = new List<(string Placeholder, string Value, string Name)>
            {
                ("{tvdbId}", series.GetProviderId(MetadataProvider.Tvdb), "tvdbId"),
                ("{imdbId}", series.GetProviderId(MetadataProvider.Imdb), "imdbId"),
                ("{tmdbId}", series.GetProviderId(MetadataProvider.Tmdb), "tmdbId")
            };
            var missing = new List<string>();
            foreach (var entry in replacements)
            {
                if (!result.Contains(entry.Placeholder, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrWhiteSpace(entry.Value))
                {
                    missing.Add(entry.Name);
                    continue;
                }
                result = result.Replace(entry.Placeholder, entry.Value, StringComparison.OrdinalIgnoreCase);
            }
            if (missing.Count > 0)
            {
                _logger.LogInformation("Skipping theme song for {seriesName}: missing provider IDs {ids}", series.Name, string.Join(", ", missing));
                link = string.Empty;
                return false;
            }
            link = result;
            return true;
        }

        public void DownloadAllThemeSongs()
        {
            var template = Plugin.Instance?.Configuration?.ThemeSongUrlTemplate;
            if (string.IsNullOrWhiteSpace(template))
            {
                _logger.LogInformation("Skipping theme song download: no URL template configured.");
                return;
            }

            int success = 0, failed = 0, skipped = 0;
            foreach (var series in GetSelectedSeries())
            {
                if (series.GetThemeSongs().Any()) { skipped++; continue; }
                if (!TryBuildThemeSongUrl(series, template, out var link)) { skipped++; continue; }
                var path = Path.Join(series.Path, "theme.mp3");
                try
                {
                    using var client = new WebClient();
                    client.DownloadFile(link, path);
                    success++;
                    _logger.LogInformation("Downloaded theme song for {seriesName}", series.Name);
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogWarning(ex, "Unable to download theme song for {seriesName} from {url}", series.Name, link);
                }
            }
            _logger.LogInformation("Theme song scan complete: {success} downloaded, {failed} failed, {skipped} skipped", success, failed, skipped);
        }

        public void Dispose() { }
    }
}