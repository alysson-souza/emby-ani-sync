#nullable enable
using System;
using emby_ani_sync.Configuration;
using emby_ani_sync.Enums;
using emby_ani_sync.Helpers;
using emby_ani_sync.Models;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace emby_ani_sync.Api
{
    // Admin endpoints

    [Route("/AniSync/buildAuthorizeRequestUrl", "GET", Summary = "Build authorize request URL")]
    [Authenticated(Roles = "Admin")]
    public class BuildAuthorizeRequestUrl : IReturn<string>
    {
        [ApiMember(Name = "provider", IsRequired = true)]
        public ApiName Provider { get; set; }

        [ApiMember(Name = "clientId", IsRequired = true)]
        public string ClientId { get; set; }

        [ApiMember(Name = "clientSecret", IsRequired = true)]
        public string ClientSecret { get; set; }

        [ApiMember(Name = "url")]
        public string? Url { get; set; }

        [ApiMember(Name = "user", IsRequired = true)]
        public Guid User { get; set; }
    }

    [Route("/AniSync/testAnimeListSaveLocation", "GET", Summary = "Test anime list save location")]
    [Authenticated(Roles = "Admin")]
    public class TestAnimeSaveLocation : IReturn<string>
    {
        [ApiMember(Name = "saveLocation", IsRequired = true)]
        public string SaveLocation { get; set; }
    }

    [Route("/AniSync/passwordGrant", "GET", Summary = "Password grant authentication")]
    [Authenticated(Roles = "Admin")]
    public class PasswordGrantAuth : IReturn<object>
    {
        [ApiMember(Name = "provider", IsRequired = true)]
        public ApiName Provider { get; set; }

        [ApiMember(Name = "userId", IsRequired = true)]
        public string UserId { get; set; }

        [ApiMember(Name = "username", IsRequired = true)]
        public string Username { get; set; }

        [ApiMember(Name = "password", IsRequired = true)]
        public string Password { get; set; }
    }

    [Route("/AniSync/user", "GET", Summary = "Get provider user")]
    [Authenticated(Roles = "Admin")]
    public class GetProviderUser : IReturn<object>
    {
        [ApiMember(Name = "apiName", IsRequired = true)]
        public ApiName ApiName { get; set; }

        [ApiMember(Name = "userId", IsRequired = true)]
        public string UserId { get; set; }
    }

    [Route("/AniSync/parameters", "GET", Summary = "Get frontend parameters")]
    [Authenticated(Roles = "Admin")]
    public class GetParameters : IReturn<object>
    {
        [ApiMember(Name = "includes")]
        public ParameterInclude[]? Includes { get; set; }

        [ApiMember(Name = "onlyConfiguredProviders")]
        public bool OnlyConfiguredProviders { get; set; }
    }

    [Route("/AniSync/sync", "POST", Summary = "Sync with provider")]
    [Authenticated(Roles = "Admin")]
    public class SyncRequest : IReturn<SyncJobStatus>
    {
        [ApiMember(Name = "provider", IsRequired = true)]
        public ApiName Provider { get; set; }

        [ApiMember(Name = "userId", IsRequired = true)]
        public string UserId { get; set; }

        [ApiMember(Name = "status", IsRequired = true)]
        public SyncHelper.Status Status { get; set; }

        [ApiMember(Name = "syncAction", IsRequired = true)]
        public SyncAction SyncAction { get; set; }
    }

    [Route("/AniSync/sync/status", "GET", Summary = "Get sync job status")]
    [Authenticated(Roles = "Admin")]
    public class GetSyncStatus : IReturn<SyncJobStatus>
    {
        [ApiMember(Name = "jobId", IsRequired = true)]
        public string JobId { get; set; }
    }

    [Route("/AniSync/deauthenticate", "GET", Summary = "Deauthenticate provider")]
    [Authenticated(Roles = "Admin")]
    public class Deauthenticate : IReturnVoid
    {
        [ApiMember(Name = "user", IsRequired = true)]
        public Guid User { get; set; }

        [ApiMember(Name = "apiName", IsRequired = true)]
        public ApiName ApiName { get; set; }
    }

    // User endpoints

    [Route("/AniSync/user/buildAuthorizeRequestUrl", "GET", Summary = "Build authorize request URL for user")]
    [Authenticated]
    public class BuildAuthorizeRequestUrlUser : IReturn<string>
    {
        [ApiMember(Name = "provider", IsRequired = true)]
        public ApiName Provider { get; set; }

        [ApiMember(Name = "user", IsRequired = true)]
        public Guid User { get; set; }
    }

    [Route("/AniSync/user/passwordGrant", "GET", Summary = "Password grant auth for user")]
    [Authenticated]
    public class PasswordGrantAuthUser : IReturn<object>
    {
        [ApiMember(Name = "provider", IsRequired = true)]
        public ApiName Provider { get; set; }

        [ApiMember(Name = "user", IsRequired = true)]
        public Guid User { get; set; }

        [ApiMember(Name = "username", IsRequired = true)]
        public string Username { get; set; }

        [ApiMember(Name = "password", IsRequired = true)]
        public string Password { get; set; }
    }

    [Route("/AniSync/user/user", "GET", Summary = "Get provider user for user")]
    [Authenticated]
    public class GetProviderUserUser : IReturn<object>
    {
        [ApiMember(Name = "apiName", IsRequired = true)]
        public ApiName ApiName { get; set; }

        [ApiMember(Name = "user", IsRequired = true)]
        public Guid User { get; set; }
    }

    [Route("/AniSync/user/configuration", "GET", Summary = "Get user configuration")]
    [Authenticated]
    public class GetConfigurationUser : IReturn<object>
    {
        [ApiMember(Name = "user", IsRequired = true)]
        public Guid User { get; set; }
    }

    [Route("/AniSync/user/configuration", "PUT", Summary = "Update user configuration")]
    [Authenticated]
    public class UpdateConfigurationUser : IReturn<object>
    {
        [ApiMember(Name = "user", IsRequired = true)]
        public Guid User { get; set; }
        public bool PlanToWatchOnly { get; set; }
        public bool RewatchCompleted { get; set; }
        public string[] LibraryToCheck { get; set; }
    }

    [Route("/AniSync/user/parameters", "GET", Summary = "Get frontend parameters for user")]
    [Authenticated]
    public class GetParametersUser : IReturn<object>
    {
        [ApiMember(Name = "includes")]
        public ParameterInclude[]? Includes { get; set; }
    }

    [Route("/AniSync/user/deauthenticate", "GET", Summary = "Deauthenticate provider for user")]
    [Authenticated]
    public class DeauthenticateUser : IReturnVoid
    {
        [ApiMember(Name = "user", IsRequired = true)]
        public Guid User { get; set; }

        [ApiMember(Name = "apiName", IsRequired = true)]
        public ApiName ApiName { get; set; }
    }

    // Anonymous endpoints

    [Route("/AniSync/authCallback", "GET", Summary = "OAuth callback")]
    [Unauthenticated]
    public class AuthCallback : IReturn<string>
    {
        [ApiMember(Name = "code", IsRequired = true)]
        public string Code { get; set; }

        [ApiMember(Name = "state")]
        public string? State { get; set; }
    }

    [Route("/AniSync/apiUrlTest", "GET", Summary = "API URL test")]
    [Unauthenticated]
    public class ApiUrlTest : IReturn<string> { }

    [Route("/AniSync/{ViewName}", "GET", Summary = "Get plugin view")]
    [Authenticated]
    public class GetView : IReturn<object>
    {
        [ApiMember(Name = "ViewName", IsRequired = true, ParameterType = "path")]
        public string ViewName { get; set; }
    }
}
