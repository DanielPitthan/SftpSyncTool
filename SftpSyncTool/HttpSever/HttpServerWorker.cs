using System.Net;
using Microsoft.Extensions.Options;
using CopyToSFTPObserver;

namespace CopyToSFTPObserver
{
    public class HttpServerWorker : BackgroundService
    {
        private readonly ILogger<HttpServerWorker> _logger;
        private readonly HttpListener _listener = new HttpListener();
        private readonly string _url;
        private readonly AppSettings _appSettings;

        public HttpServerWorker(ILogger<HttpServerWorker> logger, IOptions<AppSettings> appSettings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appSettings = appSettings?.Value ?? throw new ArgumentNullException(nameof(appSettings));
            _url = Environment.GetEnvironmentVariable("HTTP_URL") ?? "http://localhost:5050/";
            _listener.Prefixes.Add(_url);
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Iniciando servidor HTTP em {url}", _url);
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _listener.Start();
                _logger.LogInformation("Servidor HTTP pronto em {url}", _url);

                while (!stoppingToken.IsCancellationRequested)
                {
                    HttpListenerContext? ctx = null;
                    try
                    {
                        ctx = await _listener.GetContextAsync().WaitAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (HttpListenerException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao aceitar requisição HTTP: {message}", ex.Message);
                        continue;
                    }

                    _ = Task.Run(() => HandleRequestAsync(ctx, stoppingToken), stoppingToken);
                }
            }
            catch (HttpListenerException ex)
            {
                _logger.LogError(ex, "Falha ao iniciar o servidor HTTP. Verifique ACL/porta: {message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado no servidor HTTP: {message}", ex.Message);
            }
            finally
            {
                SafeStop();
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            try
            {
                var req = ctx.Request;
                var res = ctx.Response;

                if (req.HttpMethod == "GET" && (req.Url?.AbsolutePath == "/" || req.Url?.AbsolutePath == "/index.html"))
                {
                    var html = GenerateStatusPage();
                    var buffer = System.Text.Encoding.UTF8.GetBytes(html);
                    res.ContentType = "text/html; charset=utf-8";
                    res.ContentEncoding = System.Text.Encoding.UTF8;
                    res.ContentLength64 = buffer.Length;
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length, ct);
                    res.StatusCode = (int)HttpStatusCode.OK;
                }
                else if (req.HttpMethod == "GET" && req.Url?.AbsolutePath == "/logs")
                {
                    var html = GenerateLogsPage();
                    var buffer = System.Text.Encoding.UTF8.GetBytes(html);
                    res.ContentType = "text/html; charset=utf-8";
                    res.ContentEncoding = System.Text.Encoding.UTF8;
                    res.ContentLength64 = buffer.Length;
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length, ct);
                    res.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    res.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
            catch (Exception ex)
            {
                try { ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError; } catch { /* ignore */ }
                _logger.LogError(ex, "Erro ao processar requisição HTTP: {message}", ex.Message);
            }
            finally
            {
                try { ctx.Response.OutputStream.Close(); } catch { /* ignore */ }
            }
        }

        private string GenerateStatusPage()
        {
            return $@"<!DOCTYPE html>
<html lang=""pt-br"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>SftpSyncTool - Status</title>
  <style>
    body {{ font-family: Segoe UI, Arial, sans-serif; margin: 2rem; color: #222; }}
    code {{ background: #f5f5f5; padding: .2rem .4rem; border-radius: 4px; }}
    .nav-link {{ display: inline-block; margin-right: 1rem; color: #0066cc; text-decoration: none; }}
    .nav-link:hover {{ text-decoration: underline; }}
  </style>
</head>
<body>
  <nav>
    <a href=""/"" class=""nav-link"">Status</a>
    <a href=""/logs"" class=""nav-link"">Logs</a>
  </nav>
  <h1>SftpSyncTool</h1>
  <p>Serviço em execução.</p>
  <ul>
    <li>Hora do servidor: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</li>
    <li>Diretório de logs: {_appSettings.LogFile}</li>
    <li>Ambiente: {GetEnvironmentName()}</li>
  </ul>
  <p>Este endpoint é entregue pelo servidor HTTP embutido no Worker Service.</p>
</body>
</html>";
        }

        private string GenerateLogsPage()
        {
            var logContent = ReadLogFile();
            var formattedLogs = FormatLogContent(logContent);

            return $@"<!DOCTYPE html>
<html lang=""pt-br"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>SftpSyncTool - Logs</title>
  <style>
    body {{ 
      font-family: 'Courier New', monospace; 
      margin: 1rem; 
      color: #222; 
      background-color: #f8f9fa;
    }}
    .nav-link {{ 
      display: inline-block; 
      margin-right: 1rem; 
      color: #0066cc; 
      text-decoration: none; 
      font-family: Segoe UI, Arial, sans-serif;
    }}
    .nav-link:hover {{ text-decoration: underline; }}
    .log-container {{ 
      background: #fff; 
      border: 1px solid #ddd; 
      border-radius: 4px; 
      padding: 1rem; 
      margin-top: 1rem;
      max-height: 80vh;
      overflow-y: auto;
    }}
    .log-line {{ 
      margin: 0.2rem 0; 
      padding: 0.1rem 0;
      line-height: 1.4;
    }}
    .log-info {{ 
      color: #0066cc; 
      background-color: #e7f3ff;
      padding: 0.1rem 0.3rem;
      border-left: 3px solid #0066cc;
    }}
    .log-error {{ 
      color: #dc3545; 
      background-color: #ffeaea;
      padding: 0.1rem 0.3rem;
      border-left: 3px solid #dc3545;
      font-weight: bold;
    }}
    .log-warning {{ 
      color: #ffc107; 
      background-color: #fff9e6;
      padding: 0.1rem 0.3rem;
      border-left: 3px solid #ffc107;
    }}
    .log-debug {{ 
      color: #6c757d; 
      background-color: #f8f9fa;
      padding: 0.1rem 0.3rem;
      border-left: 3px solid #6c757d;
    }}
    .log-critical {{ 
      color: #fff; 
      background-color: #dc3545;
      padding: 0.1rem 0.3rem;
      border-left: 3px solid #a71e2a;
      font-weight: bold;
    }}
    .log-trace {{ 
      color: #6f42c1; 
      background-color: #f8f5ff;
      padding: 0.1rem 0.3rem;
      border-left: 3px solid #6f42c1;
    }}
    .log-timestamp {{ 
      color: #666; 
      font-weight: normal;
    }}
    .no-logs {{ 
      text-align: center; 
      color: #666; 
      font-style: italic; 
      margin: 2rem 0;
    }}
    .refresh-info {{
      font-family: Segoe UI, Arial, sans-serif;
      font-size: 0.9rem;
      color: #666;
      margin-bottom: 1rem;
    }}
  </style>
</head>
<body>
  <nav>
    <a href=""/"" class=""nav-link"">Status</a>
    <a href=""/logs"" class=""nav-link"">Logs</a>
  </nav>
  <h1 style=""font-family: Segoe UI, Arial, sans-serif;"">Logs do Sistema</h1>
  <div class=""refresh-info"">
    <p>Logs atualizados em: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | <a href=""/logs"" class=""nav-link"">Atualizar</a></p>
  </div>
  <div class=""log-container"">
    {formattedLogs}
  </div>
</body>
</html>";
        }

        private string ReadLogFile()
        {
            try
            {
                var logDirectory = _appSettings.LogFile;
                if (string.IsNullOrWhiteSpace(logDirectory))
                {
                    return "Diretório de logs não configurado.";
                }

                if (!Directory.Exists(logDirectory))
                {
                    return $"Diretório de logs não encontrado: {logDirectory}";
                }

                // Procura pelo arquivo de log mais recente com o padrão processlog_
                var logFiles = Directory.GetFiles(logDirectory, "processlog_*.log")
                                       .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                                       .ToArray();

                if (logFiles.Length == 0)
                {
                    return "Nenhum arquivo de log encontrado.";
                }

                var latestLogFile = logFiles.First();
                
                // Usa FileStream com FileShare.ReadWrite para permitir acesso compartilhado
                using (var fileStream = new FileStream(latestLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fileStream, System.Text.Encoding.UTF8))
                {
                    var lines = new List<string>();
                    string? line;
                    
                    // Lê todas as linhas
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                    
                    // Pega apenas as últimas 1000 linhas para performance
                    var lastLines = lines.TakeLast(1000).ToArray();
                    return string.Join(Environment.NewLine, lastLines);
                }
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
            {
                _logger.LogWarning("Arquivo de log está sendo usado por outro processo. Tentando leitura alternativa...");
                return TryReadLogFileAlternative();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Acesso negado ao arquivo de log: {message}", ex.Message);
                return $"Acesso negado ao arquivo de log: {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao ler arquivo de log: {message}", ex.Message);
                return $"Erro ao ler arquivo de log: {ex.Message}";
            }
        }

        private string TryReadLogFileAlternative()
        {
            try
            {
                var logDirectory = _appSettings.LogFile;
                var logFiles = Directory.GetFiles(logDirectory, "processlog_*.log")
                                       .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                                       .ToArray();

                if (logFiles.Length == 0)
                {
                    return "Nenhum arquivo de log encontrado.";
                }

                var latestLogFile = logFiles.First();
                
                // Tenta várias vezes com pequenos delays
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        // Usa FileStream com configuração mais permissiva
                        using (var fileStream = new FileStream(
                            latestLogFile, 
                            FileMode.Open, 
                            FileAccess.Read, 
                            FileShare.ReadWrite | FileShare.Delete,
                            bufferSize: 4096,
                            FileOptions.SequentialScan))
                        using (var reader = new StreamReader(fileStream, System.Text.Encoding.UTF8))
                        {
                            var content = reader.ReadToEnd();
                            var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                            var lastLines = lines.TakeLast(1000).ToArray();
                            return string.Join(Environment.NewLine, lastLines);
                        }
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        // Aguarda um pouco antes de tentar novamente
                        Thread.Sleep(100);
                        continue;
                    }
                }
                
                return "Não foi possível acessar o arquivo de log após várias tentativas. O arquivo pode estar sendo usado pelo sistema de logging.";
            }
            catch (Exception ex)
            {
                return $"Erro na leitura alternativa do arquivo de log: {ex.Message}";
            }
        }

        private string FormatLogContent(string logContent)
        {
            if (string.IsNullOrWhiteSpace(logContent))
            {
                return @"<div class=""no-logs"">Nenhum log disponível.</div>";
            }

            var lines = logContent.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            var formattedLines = new List<string>();

            foreach (var line in lines)
            {
                var formattedLine = FormatLogLine(line);
                formattedLines.Add($@"<div class=""log-line"">{formattedLine}</div>");
            }

            return string.Join(Environment.NewLine, formattedLines);
        }

        private string FormatLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return "";

            // Escape HTML characters
            line = System.Web.HttpUtility.HtmlEncode(line);

            // Determine log level and apply appropriate CSS class
            if (line.Contains("[INFO]"))
            {
                return $@"<div class=""log-info"">{line}</div>";
            }
            else if (line.Contains("[ERROR]"))
            {
                return $@"<div class=""log-error"">{line}</div>";
            }
            else if (line.Contains("[WARN]"))
            {
                return $@"<div class=""log-warning"">{line}</div>";
            }
            else if (line.Contains("[DEBUG]"))
            {
                return $@"<div class=""log-debug"">{line}</div>";
            }
            else if (line.Contains("[CRITICAL]"))
            {
                return $@"<div class=""log-critical"">{line}</div>";
            }
            else if (line.Contains("[TRACE]"))
            {
                return $@"<div class=""log-trace"">{line}</div>";
            }
            else
            {
                // Linha sem nível de log identificado (provavelmente continuação de exceção)
                return $@"<div class=""log-debug"">{line}</div>";
            }
        }

        private string GetEnvironmentName()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? 
                   Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? 
                   "Production";
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Parando servidor HTTP.");
            SafeStop();
            return base.StopAsync(cancellationToken);
        }

        private void SafeStop()
        {
            try
            {
                if (_listener.IsListening)
                    _listener.Stop();
            }
            catch { /* ignore */ }
            finally
            {
                try { _listener.Close(); } catch { /* ignore */ }
            }
        }
    }
}