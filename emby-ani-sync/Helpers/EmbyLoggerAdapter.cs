using System;
using EmbyLogger = MediaBrowser.Model.Logging.ILogger;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace emby_ani_sync.Helpers
{
    public class EmbyLoggerAdapter : Microsoft.Extensions.Logging.ILogger
    {
        private readonly EmbyLogger _embyLogger;

        public EmbyLoggerAdapter(EmbyLogger embyLogger)
        {
            _embyLogger = embyLogger;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(MsLogLevel logLevel) => true;

        public void Log<TState>(
            MsLogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter
        )
        {
            if (formatter == null)
                return;
            string message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null)
                return;

            switch (logLevel)
            {
                case MsLogLevel.Trace:
                case MsLogLevel.Debug:
                    _embyLogger.Debug(message);
                    break;
                case MsLogLevel.Information:
                    _embyLogger.Info(message);
                    break;
                case MsLogLevel.Warning:
                    _embyLogger.Warn(message);
                    break;
                case MsLogLevel.Error:
                    if (exception != null)
                        _embyLogger.ErrorException(message, exception);
                    else
                        _embyLogger.Error(message);
                    break;
                case MsLogLevel.Critical:
                    if (exception != null)
                        _embyLogger.ErrorException(message, exception);
                    else
                        _embyLogger.Error(message);
                    break;
                case MsLogLevel.None:
                    break;
            }
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();

            public void Dispose() { }
        }
    }
}
