using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Services.CustomLogger
{
    public class ConsoleLoggerProvider : ILoggerProvider, IDisposable
    {
        private readonly ConcurrentDictionary<string, ConsoleLogger> _loggers = new();
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new ConsoleLogger(name, this));
        }

        public void WriteToConsole(string coloredPrefix, string remainingMessage, ConsoleColor color)
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                try
                {
                    var originalColor = Console.ForegroundColor;
                    
                    // Escrever o prefixo com cor
                    Console.ForegroundColor = color;
                    Console.Write(coloredPrefix);
                    
                    // Escrever o resto da mensagem com cor padrão
                    Console.ForegroundColor = originalColor;
                    Console.Write(remainingMessage);
                }
                catch (Exception ex)
                {
                    // Fallback em caso de erro
                    Console.WriteLine($"Error writing to console: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}