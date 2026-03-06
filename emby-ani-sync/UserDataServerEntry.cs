#nullable enable
using System.Net.Http;
using System.Threading.Tasks;
using emby_ani_sync.Interfaces;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace emby_ani_sync
{
    public class UserDataServerEntry : IServerEntryPoint
    {
        private readonly IUserDataManager _userDataManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly IAsyncDelayer _delayer;
        private readonly TaskProcessMarkedMedia _taskProcessMarkedMedia;
        private Task? _updateTask;

        public UserDataServerEntry(
            IUserDataManager userDataManager,
            ILibraryManager libraryManager,
            IServerApplicationHost serverApplicationHost
        )
        {
            _userDataManager = userDataManager;
            _libraryManager = libraryManager;
            _serverApplicationHost = serverApplicationHost;
            _httpClientFactory = Plugin.Instance.HttpClientFactory;
            _memoryCache = Plugin.Instance.MemoryCache;
            var loggerFactory = Plugin.Instance.LoggerFactory;
            loggerFactory.CreateLogger<UpdateProviderStatus>();
            _delayer = new Delayer();
            _taskProcessMarkedMedia = new TaskProcessMarkedMedia(
                loggerFactory,
                _libraryManager,
                _memoryCache,
                _serverApplicationHost,
                _httpClientFactory,
                Plugin.Instance.AppPaths,
                _delayer
            );
        }

        public void Run()
        {
            _userDataManager.UserDataSaved += UserDataManagerOnUserDataSaved;
        }

        public void Dispose()
        {
            _userDataManager.UserDataSaved -= UserDataManagerOnUserDataSaved;
        }

        private void UserDataManagerOnUserDataSaved(object? sender, UserDataSaveEventArgs e)
        {
            if (
                e.SaveReason == UserDataSaveReason.TogglePlayed
                && Plugin.Instance?.PluginConfiguration.watchedTickboxUpdatesProvider == true
            )
            {
                if (!e.UserData.Played || e.Item is not Video)
                    return;
                Episode? episode = e.Item as Episode;
                _taskProcessMarkedMedia.AddToUpdateList((e.User.Id, episode?.Season?.Id, e.Item as Video));
                if (_updateTask == null || _updateTask.IsCompleted)
                {
                    _updateTask = _taskProcessMarkedMedia.RunUpdate();
                }
            }
        }
    }
}
