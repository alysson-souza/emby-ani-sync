using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using emby_ani_sync.Helpers;
using emby_ani_sync.Interfaces;
using emby_ani_sync.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace emby_ani_sync;

public class SyncProviderFromLocal(
    IUserManager userManager,
    ILibraryManager libraryManager,
    ILoggerFactory loggerFactory,
    IHttpClientFactory httpClientFactory,
    IApplicationPaths applicationPaths,
    IMemoryCache memoryCache,
    IAsyncDelayer delayer,
    IUserDataManager userDataManager,
    IServerApplicationHost serverApplicationHost,
    string userId,
    SyncJobTracker syncJobTracker = null,
    string jobId = null
)
{
    private readonly Guid _userId = Guid.Parse(userId);
    private readonly ILogger<SyncProviderFromLocal> _logger = loggerFactory.CreateLogger<SyncProviderFromLocal>();
    private readonly SyncJobTracker _syncJobTracker = syncJobTracker;
    private readonly string _jobId = jobId;

    public async Task SyncFromLocal()
    {
        ReportProgress("Scanning Emby library", "Collecting watched anime from the selected user's Emby library.");
        var library = SyncHelper.GetUsersLibrary(_userId, userManager, libraryManager);
        List<Series> userSeriesList = library.OfType<Series>().Select(baseItem => baseItem).ToList();
        if (userSeriesList.Count == 0)
        {
            ReportProgress("Nothing to sync", "No series were found in the selected user's Emby library.");
            return;
        }
        await GetSeasonDetails(userSeriesList);
        ReportProgress("Finishing", "Finalizing provider updates.");
    }

    private async Task GetSeasonDetails(List<Series> userSeriesList)
    {
        _logger.LogInformation($"(Sync) Starting sync to provider from local process");
        UpdateProviderStatus updateProviderStatus = new UpdateProviderStatus(
            libraryManager,
            loggerFactory,
            serverApplicationHost,
            httpClientFactory,
            applicationPaths,
            memoryCache,
            delayer
        );

        for (int seriesIndex = 0; seriesIndex < userSeriesList.Count; seriesIndex++)
        {
            Series series = userSeriesList[seriesIndex];
            ReportProgress(
                "Updating providers",
                $"Checking {series.Name} for watched progress.",
                seriesIndex + 1,
                userSeriesList.Count
            );
            _logger.LogInformation(
                $"(Sync) Retrieved {series.Name}'s seasons latest watched episode and when it was watched..."
            );
            var toMarkAsCompleted = GetMaxEpisodeAndCompletedTime(series);
            if (toMarkAsCompleted != null)
            {
                foreach (Episode episodeDateTime in toMarkAsCompleted)
                {
                    if (episodeDateTime != null)
                    {
                        try
                        {
                            await updateProviderStatus.Update(episodeDateTime, _userId, true);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"(Sync) Could not sync item; error: {e.Message}");
                            continue;
                        }

                        _logger.LogInformation("(Sync) Waiting 2 seconds before continuing...");
                        Thread.Sleep(2000);
                    }
                    else
                    {
                        _logger.LogError("(Sync) Could not get users Emby data for this season");
                    }
                }
            }
            else
            {
                _logger.LogError($"(Sync) User with ID of {_userId} not found");
            }
        }
    }

    private List<Episode> GetMaxEpisodeAndCompletedTime(Series series)
    {
        List<Episode> returnDictionary = new List<Episode>();

        _logger.LogInformation($"(Sync) Getting {series.Name} seasons latest watched episode");
        var seasons = series
            .GetChildren(new InternalItemsQuery())
            .OfType<Season>()
            .Select(baseItem => baseItem)
            .ToList();
        _logger.LogInformation($"(Sync) Series {series.Name} contains {seasons.Count} seasons");
        User user = userManager.GetUserById(_userId);
        if (user == null)
            return null;
        foreach (Season season in seasons)
        {
            _logger.LogInformation($"(Sync) Getting user data for {season.Name} of {series.Name}...");
            if (season.IndexNumber == null)
            {
                _logger.LogError($"(Sync) Season index number is null. Skipping...");
                continue;
            }

            IEnumerable<Episode> episodes = season.GetChildren(new InternalItemsQuery()).OfType<Episode>().ToArray();
            if (!episodes.Any())
            {
                _logger.LogInformation($"(Sync) No (user visible) episodes found for {season.Name} of {series.Name}");
            }
            _logger.LogInformation(
                $"(Sync) Season contains {season.GetChildren(new InternalItemsQuery()).OfType<Episode>().Count()} episodes"
            );
            Episode latestWatchedEpisode;

            try
            {
                latestWatchedEpisode = episodes
                    .Where(episode => userDataManager.GetUserData(user, episode).Played)
                    .OrderByDescending(episode => episode.IndexNumber)
                    .FirstOrDefault();
            }
            catch (Exception e)
            {
                _logger.LogError($"(Sync) Could not get user episodes watched for {season.Name}; error: {e.Message}");
                continue;
            }

            if (latestWatchedEpisode == null)
            {
                _logger.LogInformation($"(Sync) No episodes watched for {season.Name}");
                continue;
            }
            _logger.LogInformation(
                $"(Sync) The latest watched episode for this user of this season is {latestWatchedEpisode.IndexNumber}"
            );
            returnDictionary.Add(latestWatchedEpisode);
        }

        _logger.LogInformation($"(Sync) Found {returnDictionary.Count} seasons that contain user data");
        return returnDictionary;
    }

    private void ReportProgress(string phase, string message, int? current = null, int? total = null)
    {
        if (_syncJobTracker == null || string.IsNullOrWhiteSpace(_jobId))
        {
            return;
        }

        _syncJobTracker.MarkRunning(_jobId, phase, message, current, total);
    }
}
