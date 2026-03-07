using System;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace emby_ani_sync.Helpers
{
    public class EmbyLoggerFactory : ILoggerFactory
    {
        private readonly ILogManager _logManager;

        public EmbyLoggerFactory(ILogManager logManager)
        {
            _logManager = logManager;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new EmbyLoggerAdapter(_logManager.GetLogger(categoryName));
        }

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }
    }
}
