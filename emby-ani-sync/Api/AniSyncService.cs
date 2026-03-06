#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using emby_ani_sync.Api.Anilist;
using emby_ani_sync.Api.Annict;
using emby_ani_sync.Api.Kitsu;
using emby_ani_sync.Api.Shikimori;
using emby_ani_sync.Api.Simkl;
using emby_ani_sync.Configuration;
using emby_ani_sync.Enums;
using emby_ani_sync.Extensions;
using emby_ani_sync.Helpers;
using emby_ani_sync.Interfaces;
using emby_ani_sync.Models;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace emby_ani_sync.Api
{
    public class AniSyncService : IService, IRequiresRequest
    {
        public IRequest Request { get; set; }

        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ILogger<AniSyncService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IAsyncDelayer _delayer;
        private readonly ISessionContext _sessionContext;

        public AniSyncService(IApplicationHost appHost)
        {
            _serverApplicationHost = (IServerApplicationHost)appHost;
            _httpClientFactory = Plugin.Instance!.HttpClientFactory;
            _loggerFactory = Plugin.Instance.LoggerFactory;
            _logger = _loggerFactory.CreateLogger<AniSyncService>();
            _memoryCache = Plugin.Instance.MemoryCache;
            _libraryManager = appHost.Resolve<ILibraryManager>();
            _userManager = appHost.Resolve<IUserManager>();
            _userDataManager = appHost.Resolve<IUserDataManager>();
            _sessionContext = appHost.Resolve<ISessionContext>();
            _delayer = new Delayer();
        }

        private bool UserPagesEnabled()
        {
            return Plugin.Instance?.PluginConfiguration.enableUserPages == true;
        }

        private Guid GetCurrentUserId()
        {
            var session = _sessionContext.GetSession(Request);
            if (
                session != null
                && !string.IsNullOrEmpty(session.UserId)
                && Guid.TryParse(session.UserId, out var userId)
            )
            {
                return userId;
            }
            return Guid.Empty;
        }

        // Admin endpoints

        public string Get(BuildAuthorizeRequestUrl request)
        {
            _logger.LogInformation(
                "BuildAuthorizeRequestUrl called: Provider={Provider}, User={User}, Url={Url}, ClientId={ClientIdSet}",
                request.Provider,
                request.User,
                request.Url,
                !string.IsNullOrEmpty(request.ClientId)
            );
            try
            {
                var result = new ApiAuthentication(
                    request.Provider,
                    _httpClientFactory,
                    _serverApplicationHost,
                    _loggerFactory,
                    _memoryCache,
                    new ProviderApiAuth { ClientId = request.ClientId, ClientSecret = request.ClientSecret },
                    request.Url
                ).BuildAuthorizeRequestUrl(request.User);
                _logger.LogInformation("BuildAuthorizeRequestUrl result: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BuildAuthorizeRequestUrl failed");
                throw;
            }
        }

        public async Task<object> Get(TestAnimeSaveLocation request)
        {
            if (string.IsNullOrEmpty(request.SaveLocation))
                throw new ArgumentException("Save location is empty");

            try
            {
                using (
                    System.IO.File.Create(
                        Path.Combine(request.SaveLocation, Path.GetRandomFileName()),
                        1,
                        FileOptions.DeleteOnClose
                    )
                ) { }

                return string.Empty;
            }
            catch (Exception e)
            {
                throw new ArgumentException(e.Message);
            }
        }

        public async Task<object> Get(PasswordGrantAuth request)
        {
            try
            {
                new ApiAuthentication(
                    request.Provider,
                    _httpClientFactory,
                    _serverApplicationHost,
                    _loggerFactory,
                    _memoryCache,
                    new ProviderApiAuth { ClientId = request.Username, ClientSecret = request.Password }
                ).GetToken(Guid.Parse(request.UserId));
            }
            catch (Exception e)
            {
                throw new Exception($"Could not authenticate; {e.Message}");
            }

            if (request.Provider == ApiName.Kitsu)
            {
                var userConfig = Plugin.Instance?.PluginConfiguration.UserConfig.FirstOrDefault(item =>
                    item.UserId == Guid.Parse(request.UserId)
                );

                if (userConfig != null)
                {
                    KitsuApiCalls kitsuApiCalls = new KitsuApiCalls(
                        _httpClientFactory,
                        _loggerFactory,
                        _serverApplicationHost,
                        _memoryCache,
                        _delayer,
                        userConfig
                    );
                    var kitsuUserConfig = await kitsuApiCalls.GetUserInformation();
                    if (kitsuUserConfig == null)
                        throw new Exception("Could not authenticate");
                    var existingKeyPair = userConfig.KeyPairs.FirstOrDefault(item => item.Key == "KitsuUserId");
                    if (existingKeyPair != null)
                    {
                        existingKeyPair.Value = kitsuUserConfig.Id.ToString();
                    }
                    else
                    {
                        userConfig.KeyPairs.Add(
                            new KeyPairs { Key = "KitsuUserId", Value = kitsuUserConfig.Id.ToString() }
                        );
                    }

                    Plugin.Instance?.SaveConfiguration();
                }
            }

            return new object();
        }

        public async Task<object> Get(GetProviderUser request)
        {
            UserConfig? userConfig = Plugin.Instance?.PluginConfiguration.UserConfig.FirstOrDefault(item =>
                item.UserId == Guid.Parse(request.UserId)
            );
            if (userConfig == null)
            {
                _logger.LogError("User not found in config");
                throw new Exception("User not found in config");
            }

            switch (request.ApiName)
            {
                case ApiName.Mal:
                    MalApiCalls malApiCalls = new MalApiCalls(
                        _httpClientFactory,
                        _loggerFactory,
                        _serverApplicationHost,
                        _memoryCache,
                        _delayer,
                        userConfig
                    );
                    MalApiCalls.User? malUser = await malApiCalls.GetUserInformation();
                    if (malUser != null)
                        return malUser;
                    throw new Exception("Authentication failed");
                case ApiName.AniList:
                    AniListApiCalls aniListApiCalls = new AniListApiCalls(
                        _httpClientFactory,
                        _loggerFactory,
                        _serverApplicationHost,
                        _memoryCache,
                        _delayer,
                        userConfig
                    );
                    AniListViewer.Viewer? user = await aniListApiCalls.GetCurrentUser();
                    if (user == null)
                        throw new Exception("Authentication failed");
                    return new MalApiCalls.User { Name = user.Name };
                case ApiName.Kitsu:
                    KitsuApiCalls kitsuApiCalls;
                    try
                    {
                        kitsuApiCalls = new KitsuApiCalls(
                            _httpClientFactory,
                            _loggerFactory,
                            _serverApplicationHost,
                            _memoryCache,
                            _delayer,
                            userConfig
                        );
                    }
                    catch (ArgumentNullException)
                    {
                        _logger.LogError("User could not be retrieved from API");
                        throw new Exception("User could not be retrieved from API");
                    }
                    var apiCall = await kitsuApiCalls.GetUserInformation();
                    if (apiCall == null)
                        throw new Exception("Authentication failed");
                    return new MalApiCalls.User { Name = apiCall.Name };
                case ApiName.Annict:
                    Thread.Sleep(100);
                    AnnictApiCalls annictApiCalls;
                    try
                    {
                        annictApiCalls = new AnnictApiCalls(
                            _httpClientFactory,
                            _loggerFactory,
                            _serverApplicationHost,
                            _memoryCache,
                            _delayer,
                            userConfig
                        );
                    }
                    catch (ArgumentNullException)
                    {
                        _logger.LogError("User could not be retrieved from API");
                        throw new Exception("User could not be retrieved from API");
                    }
                    var annictApiCall = await annictApiCalls.GetCurrentUser();
                    if (annictApiCall == null)
                        throw new Exception("Authentication failed");
                    return new MalApiCalls.User { Name = annictApiCall.AnnictSearchData.Viewer.username };
                case ApiName.Shikimori:
                    string? shikimoriAppName = ConfigHelper.GetShikimoriAppName(_logger);
                    if (string.IsNullOrEmpty(shikimoriAppName))
                        throw new Exception("No App Name");
                    ShikimoriApiCalls shikimoriApiCalls = new ShikimoriApiCalls(
                        _httpClientFactory,
                        _loggerFactory,
                        _serverApplicationHost,
                        _memoryCache,
                        _delayer,
                        new Dictionary<string, string> { { "User-Agent", shikimoriAppName } },
                        userConfig
                    );
                    ShikimoriApiCalls.User? shikimoriUserApiCall = await shikimoriApiCalls.GetUserInformation();
                    if (shikimoriUserApiCall != null)
                        return new MalApiCalls.User { Name = shikimoriUserApiCall.Name };
                    _logger.LogError("User could not be retrieved from API");
                    throw new Exception("User could not be retrieved from API");
                case ApiName.Simkl:
                    string? simklClientId = ConfigHelper.GetSimklClientId(_logger);
                    if (string.IsNullOrEmpty(simklClientId))
                        throw new Exception("No Client ID");
                    var simklApiCalls = new SimklApiCalls(
                        _httpClientFactory,
                        _loggerFactory,
                        _serverApplicationHost,
                        _memoryCache,
                        _delayer,
                        new Dictionary<string, string> { { "simkl-api-key", simklClientId } },
                        userConfig
                    );
                    if (await simklApiCalls.GetLastActivity())
                        return new MalApiCalls.User { Name = null };
                    throw new Exception("Not authenticated");
            }

            throw new Exception("Provider not supported.");
        }

        public object Get(GetParameters request)
        {
            return GetFrontendParameters(request.Includes, request.OnlyConfiguredProviders, false);
        }

        public object Post(SyncRequest request)
        {
            SyncJobTracker syncJobTracker =
                Plugin.Instance?.SyncJobTracker ?? throw new Exception("Sync job tracker unavailable");
            string initialPhase =
                request.SyncAction == SyncAction.UpdateProvider ? "Queued provider update" : "Queued Emby update";
            string initialMessage =
                request.SyncAction == SyncAction.UpdateProvider
                    ? "Queued a manual sync from Emby to all authenticated providers."
                    : $"Queued a manual sync from {request.Provider} to Emby.";
            SyncJobStatus job = syncJobTracker.CreateJob(initialPhase, initialMessage);

            Task.Run(async () =>
            {
                try
                {
                    switch (request.SyncAction)
                    {
                        case SyncAction.UpdateProvider:
                            syncJobTracker.MarkRunning(
                                job.JobId,
                                "Preparing provider update",
                                "Preparing a manual sync from Emby to the connected providers."
                            );
                            SyncProviderFromLocal syncProviderFromLocal = new SyncProviderFromLocal(
                                _userManager,
                                _libraryManager,
                                _loggerFactory,
                                _httpClientFactory,
                                Plugin.Instance!.AppPaths,
                                _memoryCache,
                                _delayer,
                                _userDataManager,
                                _serverApplicationHost,
                                request.UserId,
                                syncJobTracker,
                                job.JobId
                            );
                            await syncProviderFromLocal.SyncFromLocal();
                            break;
                        case SyncAction.UpdateEmby:
                            syncJobTracker.MarkRunning(
                                job.JobId,
                                "Preparing Emby update",
                                $"Preparing a manual sync from {request.Provider} to Emby."
                            );
                            Sync sync = new Sync(
                                _httpClientFactory,
                                _loggerFactory,
                                _serverApplicationHost,
                                _userManager,
                                _libraryManager,
                                Plugin.Instance!.AppPaths,
                                _userDataManager,
                                _memoryCache,
                                _delayer,
                                request.Provider,
                                request.Status,
                                syncJobTracker,
                                job.JobId
                            );
                            await sync.SyncFromProvider(request.UserId);
                            break;
                    }

                    syncJobTracker.MarkCompleted(job.JobId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Manual sync job {JobId} failed", job.JobId);
                    syncJobTracker.MarkFailed(job.JobId, ex.Message, "Manual sync failed.");
                }
            });

            return job;
        }

        public object Get(GetSyncStatus request)
        {
            if (Plugin.Instance?.SyncJobTracker == null)
                throw new Exception("Sync job tracker unavailable");
            if (!Plugin.Instance.SyncJobTracker.TryGetJob(request.JobId, out SyncJobStatus jobStatus))
            {
                throw new ResourceNotFoundException("No sync job found.");
            }

            return jobStatus;
        }

        public void Get(Deauthenticate request)
        {
            DeauthenticateProvidedUser(request.User, request.ApiName);
        }

        // User endpoints

        public object Get(BuildAuthorizeRequestUrlUser request)
        {
            if (!UserPagesEnabled())
                throw new ResourceNotFoundException();
            var currentUserId = GetCurrentUserId();
            var embyUser = _userManager.GetUser(currentUserId, request.User);
            if (embyUser == null)
                throw new SecurityException("Forbidden");

            var providerApiAuth = Plugin.Instance?.PluginConfiguration.ProviderApiAuth?.FirstOrDefault(p =>
                p.Name == request.Provider
            );

            if (providerApiAuth == null)
            {
                _logger.LogError($"User {embyUser.Id} failed to build authorize request URL: Provider not configured.");
                throw new SecurityException("Forbidden");
            }

            string url = !string.IsNullOrEmpty(Plugin.Instance?.PluginConfiguration.callbackUrl)
                ? Plugin.Instance.PluginConfiguration.callbackUrl
                : "local";

            return new ApiAuthentication(
                request.Provider,
                _httpClientFactory,
                _serverApplicationHost,
                _loggerFactory,
                _memoryCache,
                new ProviderApiAuth
                {
                    ClientId = providerApiAuth.ClientId,
                    ClientSecret = providerApiAuth.ClientSecret,
                },
                url
            ).BuildAuthorizeRequestUrl(embyUser.Id);
        }

        public async Task<object> Get(PasswordGrantAuthUser request)
        {
            if (!UserPagesEnabled())
                throw new ResourceNotFoundException();
            var currentUserId = GetCurrentUserId();
            var embyUser = _userManager.GetUser(currentUserId, request.User);
            if (embyUser == null)
                throw new SecurityException("Forbidden");

            try
            {
                new ApiAuthentication(
                    request.Provider,
                    _httpClientFactory,
                    _serverApplicationHost,
                    _loggerFactory,
                    _memoryCache,
                    new ProviderApiAuth { ClientId = request.Username, ClientSecret = request.Password }
                ).GetToken(embyUser.Id);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not authenticate; {e.Message}");
            }

            if (request.Provider == ApiName.Kitsu)
            {
                var userConfig = Plugin.Instance?.PluginConfiguration.UserConfig.FirstOrDefault(item =>
                    item.UserId == embyUser.Id
                );
                if (userConfig != null)
                {
                    KitsuApiCalls kitsuApiCalls = new KitsuApiCalls(
                        _httpClientFactory,
                        _loggerFactory,
                        _serverApplicationHost,
                        _memoryCache,
                        _delayer,
                        userConfig
                    );
                    var kitsuUserConfig = await kitsuApiCalls.GetUserInformation();
                    if (kitsuUserConfig == null)
                        throw new Exception("Could not authenticate");
                    var existingKeyPair = userConfig.KeyPairs.FirstOrDefault(item => item.Key == "KitsuUserId");
                    if (existingKeyPair != null)
                    {
                        existingKeyPair.Value = kitsuUserConfig.Id.ToString();
                    }
                    else
                    {
                        userConfig.KeyPairs.Add(
                            new KeyPairs { Key = "KitsuUserId", Value = kitsuUserConfig.Id.ToString() }
                        );
                    }
                    Plugin.Instance?.SaveConfiguration();
                }
            }

            return new object();
        }

        public async Task<object> Get(GetProviderUserUser request)
        {
            if (!UserPagesEnabled())
                throw new ResourceNotFoundException();
            var currentUserId = GetCurrentUserId();
            var embyUser = _userManager.GetUser(currentUserId, request.User);
            if (embyUser == null)
                throw new SecurityException("Forbidden");

            var inner = new GetProviderUser { ApiName = request.ApiName, UserId = embyUser.Id.ToString() };
            return await Get(inner);
        }

        public object Get(GetConfigurationUser request)
        {
            if (!UserPagesEnabled())
                throw new ResourceNotFoundException();
            var currentUserId = GetCurrentUserId();
            var embyUser = _userManager.GetUser(currentUserId, request.User);
            if (embyUser == null)
                throw new SecurityException("Forbidden");

            var userConfig = Plugin.Instance?.PluginConfiguration.UserConfig.FirstOrDefault(uc =>
                uc.UserId == embyUser.Id
            );
            if (userConfig == null)
            {
                _logger.LogTrace("User not found in config, first time?");
                return new { };
            }

            return userConfig;
        }

        public object Put(UpdateConfigurationUser request)
        {
            if (!UserPagesEnabled())
                throw new ResourceNotFoundException();
            var currentUserId = GetCurrentUserId();
            var embyUser = _userManager.GetUser(currentUserId, request.User);
            if (embyUser == null)
                throw new SecurityException("Forbidden");

            var plugin = Plugin.Instance;
            if (plugin?.PluginConfiguration == null)
                throw new Exception("Plugin configuration not loaded");

            var config = plugin.PluginConfiguration;
            config.UserConfig = config.UserConfig ?? Array.Empty<UserConfig>();

            var userConfig = config.UserConfig.FirstOrDefault(x => x.UserId == embyUser.Id);

            HashSet<string> libraryIds = new HashSet<string>(request.LibraryToCheck);

            if (!_libraryManager.UserHasAccessToLibraries(libraryIds, embyUser))
            {
                _logger.LogError(
                    $"User {embyUser.Id} does not have access to requested libraries ({string.Join(", ", request.LibraryToCheck)})"
                );
                throw new SecurityException("Forbidden");
            }

            if (userConfig == null)
            {
                userConfig = new UserConfig
                {
                    UserId = embyUser.Id,
                    PlanToWatchOnly = request.PlanToWatchOnly,
                    RewatchCompleted = request.RewatchCompleted,
                    LibraryToCheck = request.LibraryToCheck,
                };

                config.UserConfig = config.UserConfig.Append(userConfig).ToArray();
            }
            else
            {
                userConfig.PlanToWatchOnly = request.PlanToWatchOnly;
                userConfig.RewatchCompleted = request.RewatchCompleted;
                userConfig.LibraryToCheck = request.LibraryToCheck;
            }

            plugin.SaveConfiguration();
            return userConfig;
        }

        public object Get(GetParametersUser request)
        {
            if (!UserPagesEnabled())
                throw new ResourceNotFoundException();
            return GetFrontendParameters(request.Includes, true, true);
        }

        public void Get(DeauthenticateUser request)
        {
            if (!UserPagesEnabled())
                throw new ResourceNotFoundException();
            DeauthenticateProvidedUser(request.User, request.ApiName);
        }

        // Anonymous endpoints

        public object Get(AuthCallback request)
        {
            if (request.State == null)
                throw new ArgumentException("State is empty");
            StoredState? storedState = MemoryCacheHelper.ConsumeState(_memoryCache, request.State);
            if (storedState == null)
                throw new ArgumentException("User not found or link already used/expired, try again");
            new ApiAuthentication(
                storedState.ApiName,
                _httpClientFactory,
                _serverApplicationHost,
                _loggerFactory,
                _memoryCache
            ).GetToken(storedState.UserId, request.Code);

            if (!string.IsNullOrEmpty(Plugin.Instance?.PluginConfiguration.callbackRedirectUrl))
            {
                string replacedCallbackRedirectUrl = Plugin
                    .Instance.PluginConfiguration.callbackRedirectUrl.Replace("{{LocalIpAddress}}", "localhost")
                    .Replace(
                        "{{LocalPort}}",
                        _serverApplicationHost.EnableHttps
                            ? _serverApplicationHost.HttpsPort.ToString()
                            : _serverApplicationHost.HttpPort.ToString()
                    );

                if (Uri.TryCreate(replacedCallbackRedirectUrl, UriKind.Absolute, out _))
                {
                    // Emby doesn't have Redirect(), so we write the redirect manually
                    Request.Response.StatusCode = 302;
                    Request.Response.AddHeader("Location", replacedCallbackRedirectUrl);
                    return null;
                }
                else
                {
                    _logger.LogWarning($"Invalid redirect URL ({replacedCallbackRedirectUrl}), skipping redirect.");
                }
            }

            return "Success! Received access token! You can test your authentication in Ani-Sync Configuration!";
        }

        public string Get(ApiUrlTest request)
        {
            return "This is the correct URL.";
        }

        public object Get(GetView request)
        {
            if (Plugin.Instance == null)
                throw new ArgumentException("No plugin instance found");
            if (!UserPagesEnabled())
                throw new ResourceNotFoundException();

            IEnumerable<PluginPageInfo> pages = Plugin.Instance.GetViews();
            if (pages == null)
                throw new ResourceNotFoundException("Pages is null or empty");

            PluginPageInfo? view = pages.FirstOrDefault(pageInfo => pageInfo?.Name == request.ViewName);
            if (view == null)
                throw new ResourceNotFoundException("No matching view found");

            Stream? stream = Plugin.Instance.GetType().Assembly.GetManifestResourceStream(view.EmbeddedResourcePath);
            if (stream == null)
            {
                _logger.LogError($"Failed to get resource {view.EmbeddedResourcePath}");
                throw new ResourceNotFoundException();
            }

            var mimeType = MimeTypes.GetMimeType(view.EmbeddedResourcePath);
            Request.Response.ContentType = mimeType;
            return stream;
        }

        // Helper methods

        private Parameters GetFrontendParameters(
            ParameterInclude[]? includes,
            bool onlyConfiguredProviders,
            bool onlyLibrariesUserHasAccessTo
        )
        {
            Parameters toReturn = new Parameters();

            if (includes == null || includes.Contains(ParameterInclude.ProviderList))
            {
                var configuredList = Plugin
                    .Instance?.PluginConfiguration.ProviderApiAuth?.Where(x => !string.IsNullOrWhiteSpace(x.ClientId))
                    .Select(x => x.Name)
                    .ToList();
                var configured =
                    configuredList != null ? new HashSet<ApiName>(configuredList) : (HashSet<ApiName>?)null;

                if (configured != null)
                {
                    configured.Add(ApiName.Kitsu);
                }
                else
                {
                    configured = new HashSet<ApiName> { ApiName.Kitsu };
                }

                toReturn.providerList = new List<ExpandoObject>();

                foreach (ApiName apiName in Enum.GetValues(typeof(ApiName)))
                {
                    if (onlyConfiguredProviders && (configured == null || !configured.Contains(apiName)))
                        continue;

                    dynamic provider = new ExpandoObject();
                    provider.Name = apiName
                        .GetType()
                        .GetMember(apiName.ToString())
                        .First()
                        .GetCustomAttribute<DisplayAttribute>()
                        ?.GetName();
                    provider.Key = apiName;

                    toReturn.providerList.Add(provider);
                }
            }

            if (includes == null || includes.Contains(ParameterInclude.LocalIpAddress))
                toReturn.localIpAddress = "localhost";

            if (includes == null || includes.Contains(ParameterInclude.LocalPort))
                toReturn.localPort = _serverApplicationHost.EnableHttps
                    ? _serverApplicationHost.HttpsPort
                    : _serverApplicationHost.HttpPort;

            if (includes == null || includes.Contains(ParameterInclude.Https))
                toReturn.https = _serverApplicationHost.EnableHttps;

            if (includes == null || includes.Contains(ParameterInclude.Libraries))
            {
                Dictionary<string, string> libraries = new Dictionary<string, string>();
                if (onlyLibrariesUserHasAccessTo)
                {
                    var currentUserId = GetCurrentUserId();
                    User? embyUser = _userManager.GetUser(currentUserId, null);
                    if (embyUser != null)
                    {
                        libraries = _libraryManager.GetLibrariesUserHasAccessTo(embyUser);
                    }
                }
                else
                {
                    libraries = _libraryManager
                        .GetVirtualFolders()
                        .ToDictionary(
                            virtualFolderInfo => virtualFolderInfo.ItemId,
                            virtualFolderInfo => virtualFolderInfo.Name
                        );
                }
                toReturn.libraries = new List<ExpandoObject>();
                foreach (var library in libraries)
                {
                    dynamic lib = new ExpandoObject();
                    lib.Name = library.Value;
                    lib.Id = library.Key;
                    toReturn.libraries.Add(lib);
                }
            }

            return toReturn;
        }

        private void DeauthenticateProvidedUser(Guid user, ApiName apiName)
        {
            var currentUserId = GetCurrentUserId();
            var embyUser = _userManager.GetUser(currentUserId, user);
            if (embyUser == null)
                throw new SecurityException("Forbidden");

            (bool success, string? reason) deauthenticateUser = ConfigHelper.DeauthenticateUser(embyUser.Id, apiName);
            if (!deauthenticateUser.success)
            {
                _logger.LogError($"Error while deauthenticating user {embyUser.Id}: {deauthenticateUser.reason}");
                throw new Exception($"Error while deauthenticating user {embyUser.Id}: {deauthenticateUser.reason}");
            }
        }
    }
}
