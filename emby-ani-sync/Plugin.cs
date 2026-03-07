#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using emby_ani_sync.Configuration;
using emby_ani_sync.Helpers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace emby_ani_sync
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogManager logManager)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            AppPaths = applicationPaths;
            LoggerFactory = new EmbyLoggerFactory(logManager);

            var services = new ServiceCollection();
            services.AddHttpClient();
            var provider = services.BuildServiceProvider();
            HttpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

            MemoryCache = new MemoryCache(new MemoryCacheOptions());
            SyncJobTracker = new SyncJobTracker(LoggerFactory);
        }

        public override string Name => "Ani-Sync";
        public override Guid Id => Guid.Parse("c78f11cf-93e6-4423-8c42-d2c255b70e47");
        public override string Description => "Synchronize anime watch status between Emby and anime tracking sites.";
        public PluginConfiguration PluginConfiguration => Configuration;
        public IApplicationPaths AppPaths { get; }
        public static Plugin? Instance { get; private set; }
        public ILoggerFactory LoggerFactory { get; }
        public IHttpClientFactory HttpClientFactory { get; }
        public IMemoryCache MemoryCache { get; }
        public SyncJobTracker SyncJobTracker { get; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.ConfigPage.html",
                },
                new PluginPageInfo
                {
                    Name = "AniSync_ConfigPageJs",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.ConfigPageJs.js",
                },
                new PluginPageInfo
                {
                    Name = "AniSync_CommonJs",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.CommonJs.js",
                },
                new PluginPageInfo
                {
                    Name = "AniSync_ManualSync",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.ManualSync.html",
                },
                new PluginPageInfo
                {
                    Name = "AniSync_ManualSyncJs",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.ManualSyncJs.js",
                },
            };
        }

        public IEnumerable<PluginPageInfo> GetViews()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "settings",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.ConfigPageUser.html",
                },
            };
        }
    }
}
