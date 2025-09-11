using System.Text;
using Microsoft.Extensions.Logging;

namespace Services.CustomLogger
{
    public class FileLogger : ILogger
    {
        private readonly string _name;
        private readonly FileLoggerConfiguration _config;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string name, FileLoggerConfiguration config, FileLoggerProvider provider)
        {
            _name = name;
            _config = config;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message))
                return;

            var logEntry = FormatLogEntry(logLevel, message, exception);
            _provider.WriteLog(logEntry);
        }

        private string FormatLogEntry(LogLevel logLevel, string message, Exception? exception)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var level = GetLogLevelString(logLevel);

            var logBuilder = new StringBuilder();
            logBuilder.AppendLine($"[{timestamp}] [{level}] {message}");

            if (exception != null)
            {
                logBuilder.AppendLine($"Exception: {exception}");
            }

            return logBuilder.ToString();
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRITICAL",
                _ => "UNKNOWN"
            };
        }
    }
}