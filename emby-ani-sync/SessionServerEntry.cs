using System;
using System.Net.Http;
using emby_ani_sync.Interfaces;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace emby_ani_sync
{
    public class SessionServerEntry : IServerEntryPoint
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<SessionServerEntry> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IAsyncDelayer _delayer;

        public SessionServerEntry(
            ISessionManager sessionManager,
            ILibraryManager libraryManager,
            IServerApplicationHost serverApplicationHost
        )
        {
            _sessionManager = sessionManager;
            _libraryManager = libraryManager;
            _serverApplicationHost = serverApplicationHost;
            _httpClientFactory = Plugin.Instance.HttpClientFactory;
            _loggerFactory = Plugin.Instance.LoggerFactory;
            _logger = _loggerFactory.CreateLogger<SessionServerEntry>();
            _memoryCache = Plugin.Instance.MemoryCache;
            _delayer = new Delayer();
        }

        public void Run()
        {
            _sessionManager.PlaybackStopped += PlaybackStopped;
        }

        public void Dispose()
        {
            _sessionManager.PlaybackStopped -= PlaybackStopped;
        }

        public async void PlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            try
            {
                UpdateProviderStatus updateProviderStatus = new UpdateProviderStatus(
                    _libraryManager,
                    _loggerFactory,
                    _serverApplicationHost,
                    _httpClientFactory,
                    Plugin.Instance.AppPaths,
                    _memoryCache,
                    _delayer
                );
                foreach (var user in e.Users)
                {
                    await updateProviderStatus.Update(e.Item, user.Id, e.PlayedToCompletion);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError($"Fatal error occured during anime sync job: {exception}");
            }
        }
    }
}
