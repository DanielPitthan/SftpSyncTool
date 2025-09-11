using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Services.CustomLogger
{
    public class FileLoggerProvider : ILoggerProvider, IDisposable
    {
        private readonly FileLoggerConfiguration _config;
        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
        private readonly object _lockObject = new object();
        private string? _currentLogFile;
        private StreamWriter? _streamWriter;
        private bool _disposed = false;

        public FileLoggerProvider(FileLoggerConfiguration config)
        {
            _config = config;
            EnsureLogDirectoryExists();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _config, this));
        }

        public void WriteLog(string logEntry)
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                try
                {
                    EnsureLogFileIsReady();
                    _streamWriter?.Write(logEntry);
                    _streamWriter?.Flush();

                    // Verificar se o arquivo atingiu o tamanho máximo
                    if (_streamWriter != null && _streamWriter.BaseStream.Length >= _config.MaxFileSizeInBytes)
                    {
                        RotateLogFile();
                    }
                }
                catch (Exception ex)
                {
                    // Em caso de erro, tenta escrever no console como fallback
                    Console.WriteLine($"Error writing to log file: {ex.Message}");
                    Console.WriteLine($"Log entry: {logEntry}");
                }
            }
        }

        private void EnsureLogDirectoryExists()
        {
            var logDirectory = GetLogDirectory();
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
        }

        private string GetLogDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.LogDirectory);
        }

        private void EnsureLogFileIsReady()
        {
            if (_streamWriter == null || _currentLogFile == null)
            {
                CreateNewLogFile();
            }
        }

        private void CreateNewLogFile()
        {
            CloseCurrentLogFile();

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{_config.LogFilePrefix}_{timestamp}.log";
            _currentLogFile = Path.Combine(GetLogDirectory(), fileName);

            _streamWriter = new StreamWriter(_currentLogFile, append: true, encoding: Encoding.UTF8)
            {
                AutoFlush = true
            };
        }

        private void RotateLogFile()
        {
            CloseCurrentLogFile();

            // Excluir arquivo antigo se existir
            if (_currentLogFile != null && File.Exists(_currentLogFile))
            {
                try
                {
                    File.Delete(_currentLogFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting old log file: {ex.Message}");
                }
            }

            // Criar novo arquivo
            CreateNewLogFile();
        }

        private void CloseCurrentLogFile()
        {
            _streamWriter?.Close();
            _streamWriter?.Dispose();
            _streamWriter = null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lockObject)
                {
                    CloseCurrentLogFile();
                    _disposed = true;
                }
            }
        }
    }
}