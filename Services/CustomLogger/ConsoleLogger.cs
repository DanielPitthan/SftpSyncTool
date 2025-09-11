using System.Text;
using Microsoft.Extensions.Logging;

namespace Services.CustomLogger
{
    public class ConsoleLogger : ILogger
    {
        private readonly string _name;
        private readonly ConsoleLoggerProvider _provider;

        public ConsoleLogger(string name, ConsoleLoggerProvider provider)
        {
            _name = name;
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

            var (coloredPrefix, remainingMessage) = FormatLogEntry(logLevel, message, exception);
            var color = GetLogLevelColor(logLevel);
            _provider.WriteToConsole(coloredPrefix, remainingMessage, color);
        }

        private (string coloredPrefix, string remainingMessage) FormatLogEntry(LogLevel logLevel, string message, Exception? exception)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var level = GetLogLevelString(logLevel);

            var coloredPrefix = $"[{timestamp}] [{level}]";
            var remainingMessage = $" {message}";

            if (exception != null)
            {
                remainingMessage += Environment.NewLine + $"Exception: {exception}";
            }

            remainingMessage += Environment.NewLine;

            return (coloredPrefix, remainingMessage);
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

        private static ConsoleColor GetLogLevelColor(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => ConsoleColor.Gray,
                LogLevel.Debug => ConsoleColor.Cyan,
                LogLevel.Information => ConsoleColor.Green,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };
        }
    }
}