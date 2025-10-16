using System.Text;

namespace Infrastructure.Email
{
    /// <summary>
    /// Construtor de notificações por e-mail HTML formatadas e estilizadas
    /// </summary>
    public class EmailNotificationBuilder
    {
        private readonly TaskExecutionReport _report;
        
        public EmailNotificationBuilder(TaskExecutionReport report)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
        }

        /// <summary>
        /// Constrói o corpo do e-mail em HTML formatado
        /// </summary>
        /// <returns>HTML do corpo do e-mail</returns>
        public string BuildHtmlBody()
        {
            var sb = new StringBuilder();
            
            // Cabeçalho do documento
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"pt-BR\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1.0\">");
            sb.AppendLine("<title>Relatório de Processamento</title>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body style=\"font-family:Arial,sans-serif;margin:0;padding:20px;background:#f4f4f4\">");
            
            // Container principal
            sb.AppendLine("<div style=\"max-width:600px;margin:0 auto;background:#fff;border-radius:8px;box-shadow:0 2px 4px rgba(0,0,0,0.1)\">");
            
            // Cabeçalho
            AppendHeader(sb);
            
            // Conteúdo
            sb.AppendLine("<div style=\"padding:20px\">");
            
            // Arquivos processados
            if (_report.FilesProcessed.Any())
            {
                AppendFilesProcessedSection(sb);
            }
            
            // Arquivos com erro
            if (_report.FilesWithError.Any())
            {
                AppendFilesWithErrorSection(sb);
            }
            
            // Detalhes de execução
            if (_report.ExecutionDetails.Any())
            {
                AppendExecutionDetailsSection(sb);
            }
            
            sb.AppendLine("</div>"); // Fim do conteúdo
            
            // Rodapé
            AppendFooter(sb);
            
            sb.AppendLine("</div>"); // Fim do container
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }

        private void AppendHeader(StringBuilder sb)
        {
            sb.AppendLine("<div style=\"background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);color:#fff;padding:20px;border-radius:8px 8px 0 0\">");
            sb.AppendLine("<h1 style=\"margin:0;font-size:24px\">Relatório de Processamento</h1>");
            sb.AppendLine($"<p style=\"margin:5px 0 0;font-size:14px;opacity:0.9\">{_report.FolderName}</p>");
            sb.AppendLine($"<p style=\"margin:5px 0 0;font-size:12px;opacity:0.8\">{_report.ExecutionDate:dd/MM/yyyy HH:mm:ss}</p>");
            sb.AppendLine("</div>");
        }

        private void AppendFilesProcessedSection(StringBuilder sb)
        {
            sb.AppendLine("<div style=\"margin-bottom:20px\">");
            sb.AppendLine("<h2 style=\"color:#667eea;font-size:18px;margin:0 0 10px;border-bottom:2px solid #667eea;padding-bottom:5px\">✓ Arquivos Processados</h2>");
            sb.AppendLine("<table style=\"width:100%;border-collapse:collapse;font-size:14px\">");
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr style=\"background:#f8f9fa\">");
            sb.AppendLine("<th style=\"padding:10px;text-align:left;border:1px solid #dee2e6;font-weight:600\">#</th>");
            sb.AppendLine("<th style=\"padding:10px;text-align:left;border:1px solid #dee2e6;font-weight:600\">Arquivo</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");
            
            for (int i = 0; i < _report.FilesProcessed.Count; i++)
            {
                var bgColor = i % 2 == 0 ? "#fff" : "#f8f9fa";
                sb.AppendLine($"<tr style=\"background:{bgColor}\">");
                sb.AppendLine($"<td style=\"padding:8px;border:1px solid #dee2e6\">{i + 1}</td>");
                sb.AppendLine($"<td style=\"padding:8px;border:1px solid #dee2e6\">{_report.FilesProcessed[i]}</td>");
                sb.AppendLine("</tr>");
            }
            
            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
            sb.AppendLine($"<p style=\"margin:10px 0 0;font-size:12px;color:#6c757d\">Total: {_report.FilesProcessed.Count} arquivo(s)</p>");
            sb.AppendLine("</div>");
        }

        private void AppendFilesWithErrorSection(StringBuilder sb)
        {
            sb.AppendLine("<div style=\"margin-bottom:20px\">");
            sb.AppendLine("<h2 style=\"color:#dc3545;font-size:18px;margin:0 0 10px;border-bottom:2px solid #dc3545;padding-bottom:5px\">✗ Arquivos com Erro</h2>");
            sb.AppendLine("<table style=\"width:100%;border-collapse:collapse;font-size:14px\">");
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr style=\"background:#fff5f5\">");
            sb.AppendLine("<th style=\"padding:10px;text-align:left;border:1px solid #f8d7da;font-weight:600\">#</th>");
            sb.AppendLine("<th style=\"padding:10px;text-align:left;border:1px solid #f8d7da;font-weight:600\">Arquivo</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");
            
            for (int i = 0; i < _report.FilesWithError.Count; i++)
            {
                var bgColor = i % 2 == 0 ? "#fff" : "#fff5f5";
                sb.AppendLine($"<tr style=\"background:{bgColor}\">");
                sb.AppendLine($"<td style=\"padding:8px;border:1px solid #f8d7da\">{i + 1}</td>");
                sb.AppendLine($"<td style=\"padding:8px;border:1px solid #f8d7da\">{_report.FilesWithError[i]}</td>");
                sb.AppendLine("</tr>");
            }
            
            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
            sb.AppendLine($"<p style=\"margin:10px 0 0;font-size:12px;color:#6c757d\">Total: {_report.FilesWithError.Count} arquivo(s)</p>");
            sb.AppendLine("</div>");
        }

        private void AppendExecutionDetailsSection(StringBuilder sb)
        {
            sb.AppendLine("<div style=\"margin-bottom:20px\">");
            sb.AppendLine("<h2 style=\"color:#667eea;font-size:18px;margin:0 0 10px;border-bottom:2px solid #667eea;padding-bottom:5px\">Detalhes de Execução</h2>");
            
            foreach (var detail in _report.ExecutionDetails)
            {
                var statusColor = detail.Success ? "#28a745" : "#dc3545";
                var statusIcon = detail.Success ? "✓" : "✗";
                
                sb.AppendLine("<div style=\"margin-bottom:15px;padding:12px;background:#f8f9fa;border-left:4px solid " + statusColor + ";border-radius:4px\">");
                sb.AppendLine($"<div style=\"display:flex;align-items:center;margin-bottom:5px\">");
                sb.AppendLine($"<span style=\"color:{statusColor};font-size:18px;margin-right:8px\">{statusIcon}</span>");
                sb.AppendLine($"<strong style=\"color:#495057;font-size:14px\">{detail.TaskType}</strong>");
                sb.AppendLine($"<span style=\"margin-left:auto;font-size:12px;color:#6c757d\">{detail.Timestamp:HH:mm:ss}</span>");
                sb.AppendLine("</div>");
                sb.AppendLine($"<p style=\"margin:0;font-size:13px;color:#6c757d\">{detail.Message}</p>");
                sb.AppendLine("</div>");
            }
            
            sb.AppendLine("</div>");
        }

        private void AppendFooter(StringBuilder sb)
        {
            sb.AppendLine("<div style=\"background:#f8f9fa;padding:15px;border-radius:0 0 8px 8px;text-align:center;border-top:1px solid #dee2e6\">");
            sb.AppendLine("<p style=\"margin:0;font-size:12px;color:#6c757d\">Este é um e-mail automático. Por favor, não responda.</p>");
            sb.AppendLine("<p style=\"margin:5px 0 0;font-size:11px;color:#adb5bd\">SFTP Sync Tool - Monitoramento de Transferências</p>");
            sb.AppendLine("<p style=\"margin:5px 0 0;font-size:11px;color:#adb5bd\">Github: https://github.com/DanielPitthan/SftpSyncTool</p>");
            sb.AppendLine("</div>");
        }
    }
}
