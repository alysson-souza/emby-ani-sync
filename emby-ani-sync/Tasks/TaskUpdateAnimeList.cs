using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Tasks;

namespace emby_ani_sync
{
    public class TaskUpdateAnimeList : IScheduledTask
    {
        private readonly IApplicationPaths _applicationPaths;

        public string Name => "AniSync Update Anime List";
        public string Key => "UpdateAnimeList";
        public string Description => "Update the anime list to the latest version.";
        public string Category => "AniSync";

        public TaskUpdateAnimeList(IApplicationPaths applicationPaths)
        {
            _applicationPaths = applicationPaths;
        }

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            return Task.Run(
                async () =>
                {
                    UpdateAnimeList updateAnimeList = new UpdateAnimeList(
                        Plugin.Instance.HttpClientFactory,
                        Plugin.Instance.LoggerFactory,
                        _applicationPaths
                    );
                    await updateAnimeList.Update();
                },
                cancellationToken
            );
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var trigger = new TaskTriggerInfo { Type = "IntervalTrigger", IntervalTicks = TimeSpan.FromDays(1).Ticks };

            return new[] { trigger };
        }
    }
}
