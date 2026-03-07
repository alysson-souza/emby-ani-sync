using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace emby_ani_sync.Helpers;

public class SyncHelper
{
    public static IReadOnlyList<BaseItem> GetUsersLibrary(
        Guid userId,
        IUserManager userManager,
        ILibraryManager libraryManager
    )
    {
        var query = new InternalItemsQuery(userManager.GetUserById(userId))
        {
            IncludeItemTypes = new[] { typeof(Movie).Name, typeof(Series).Name },
            IsVirtualItem = false,
        };

        return libraryManager.GetItemList(query);
    }

    /// <summary>
    /// Get the preferred series provider ID.
    /// </summary>
    public static (AnimeOfflineDatabaseHelpers.Source? source, int? providerId) GetSeriesProviderId(Series series)
    {
        if (
            series.ProviderIds.ContainsKey("AniList") && int.TryParse(series.ProviderIds["AniList"], out int providerId)
        )
        {
            return (AnimeOfflineDatabaseHelpers.Source.Anilist, providerId);
        }
        else if (series.ProviderIds.ContainsKey("AniDB") && int.TryParse(series.ProviderIds["AniDB"], out providerId))
        {
            return (AnimeOfflineDatabaseHelpers.Source.Anidb, providerId);
        }

        return (null, null);
    }

    /// <summary>
    /// Get a list of seasons corresponding with their provider ID.
    /// </summary>
    /// <param name="convertedWatchList">List of metadata to match to seasons.</param>
    /// <param name="providerId">AniList provider ID.</param>
    /// <param name="series">Series to retrieve season of.</param>
    /// <param name="source">Metadata source.</param>
    /// <returns>List of seasons with their completed date and progress.</returns>
    public static async Task<List<(Season, DateTime?, int)>> GetLocalSeasons(
        List<Sync.SyncAnimeMetadata> convertedWatchList,
        int providerId,
        Series series,
        AnimeOfflineDatabaseHelpers.Source source,
        ILogger logger,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IApplicationPaths applicationPaths
    )
    {
        List<(Season, DateTime?, int)> seasons = new List<(Season, DateTime?, int)>();

        List<AnimeListAnimeOfflineDatabaseCombo> seasonIdsFromLocal = await GetSeasonIdsFromLocal(
            providerId,
            source,
            logger,
            loggerFactory,
            httpClientFactory,
            applicationPaths
        );

        if (seasonIdsFromLocal != null)
        {
            foreach (AnimeListAnimeOfflineDatabaseCombo animeListAnime in seasonIdsFromLocal)
            {
                if (animeListAnime.AnimeListAnime != null && animeListAnime.AnimeListAnime.Anidbid != null)
                {
                    var found = convertedWatchList.FirstOrDefault(item =>
                        item?.ids?.AniDb != null
                        && int.TryParse(animeListAnime?.AnimeListAnime?.Anidbid, out int convertedAniDbId)
                        && item.ids.AniDb == convertedAniDbId
                    );
                    if (found != null)
                    {
                        var season = series
                            .GetChildren(new InternalItemsQuery())
                            .Select(season => season as Season)
                            .FirstOrDefault(season => season?.IndexNumber == found.season);
                        if (season != null)
                        {
                            seasons.Add(
                                (season, (DateTime?)(found.completedAt ?? DateTime.UtcNow), found.episodesWatched)
                            );
                            logger.LogInformation("(Sync) Retrieved");
                        }
                        else
                        {
                            logger.LogWarning("(Sync) Could not retrieve season from local library");
                        }
                    }
                }
                else
                {
                    logger.LogWarning("(Sync) Could not retrieve AniDb ID from this season");
                }
            }
        }

        return seasons;
    }

    /// <summary>
    /// Get season IDs from local library.
    /// </summary>
    /// <param name="providerId">ID of the provider.</param>
    /// <param name="seasonFilter">Optional list of season numbers to filter by.</param>
    /// <returns>List of season IDs.</returns>
    public static async Task<List<AnimeListAnimeOfflineDatabaseCombo>> GetSeasonIdsFromLocal(
        int providerId,
        AnimeOfflineDatabaseHelpers.Source source,
        ILogger logger,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IApplicationPaths applicationPaths,
        List<int> seasonFilter = null
    )
    {
        var id = await AnimeOfflineDatabaseHelpers.GetProviderIdsFromMetadataProvider(
            httpClientFactory.CreateClient(),
            logger,
            providerId,
            source
        );
        if (id is { AniDb: { } })
        {
            AnimeListHelpers.AnimeListXml animeListXml = await AnimeListHelpers.GetAnimeListFileContents(
                logger,
                loggerFactory,
                httpClientFactory,
                applicationPaths
            );
            var seasons = AnimeListHelpers.ListAllSeasonOfAniDbSeries(id.AniDb.Value, animeListXml);
            List<AnimeListHelpers.AnimeListAnime> seasonsList;
            if (seasonFilter != null)
            {
                IEnumerable<AnimeListHelpers.AnimeListAnime> listAnimeSeasons = seasons.ToList();
                seasonsList = listAnimeSeasons
                    .Where(season =>
                        int.TryParse(season.Defaulttvdbseason, out int parsedSeasonNumber)
                        && seasonFilter.Contains(parsedSeasonNumber)
                    )
                    .ToList();
                if (seasonsList.Count == 0)
                {
                    seasonsList = listAnimeSeasons.Where(season => season.Defaulttvdbseason == "a").ToList();
                }
            }
            else if (seasons != null)
            {
                seasonsList = seasons.ToList();
            }
            else
            {
                logger.LogWarning("(Sync) Season could not be retrieved from Emby; skipping...");
                return null;
            }

            if (seasonsList.Count() > 1)
            {
                return seasonsList
                    .Select(animeListAnime => new AnimeListAnimeOfflineDatabaseCombo
                    {
                        AnimeListAnime = animeListAnime,
                    })
                    .ToList();
            }
            else
            {
                var seasonMetadataCombo = new List<AnimeListAnimeOfflineDatabaseCombo>
                {
                    new() { AnimeListAnime = seasonsList.First(), OfflineDatabaseResponse = id },
                };
                return seasonMetadataCombo;
            }
        }

        return null;
    }

    public class AnimeListAnimeOfflineDatabaseCombo
    {
        public AnimeListHelpers.AnimeListAnime AnimeListAnime { get; set; }
        public AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse OfflineDatabaseResponse { get; set; }
    }

    public enum Status
    {
        Completed,
        Watching,
        Both,
    }
}
