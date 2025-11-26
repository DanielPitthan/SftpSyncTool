using Microsoft.Extensions.Logging;
using Models.MappingTasks;

namespace Infrastructure
{
    public class AppTaskMapperConfigurator : IAppTaskMapperConfigurator
    {
        private readonly ILogger<AppTaskMapperConfigurator> _logger;

        public AppTaskMapperConfigurator(ILogger<AppTaskMapperConfigurator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Mapeia as tarefas do arquivo de configuração JSON
        /// </summary>
        /// <returns>AppTask ou null se não conseguir mapear</returns>
        public AppTask? MapAppTask()
        {
            const string configFileName = "apptasks.json";

            try
            {
                if (!File.Exists(configFileName))
                {
                    _logger.LogWarning("O arquivo de configuração '{fileName}' não existe.", configFileName);
                    return null;
                }

                string json;
                try
                {
                    json = File.ReadAllText(configFileName);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError("Acesso negado ao ler o arquivo '{fileName}': {message}", configFileName, ex.Message);
                    return null;
                }
                catch (IOException ex)
                {
                    _logger.LogError("Erro de I/O ao ler o arquivo '{fileName}': {message}", configFileName, ex.Message);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogError("O arquivo '{fileName}' está vazio ou contém apenas espaços em branco.", configFileName);
                    return null;
                }

                // Validação básica de JSON antes de deserializar
                if (!IsValidJsonStructure(json))
                {
                    _logger.LogError("O arquivo '{fileName}' não contém uma estrutura JSON válida.", configFileName);
                    return null;
                }

                AppTask? appTask = null;
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };

                    appTask = System.Text.Json.JsonSerializer.Deserialize<AppTask>(json, options);
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogError("Erro ao deserializar JSON do arquivo '{fileName}': {message}", configFileName, ex.Message);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro inesperado ao deserializar JSON do arquivo '{fileName}': {message}", configFileName, ex.Message);
                    return null;
                }

                if (appTask == null)
                {
                    _logger.LogError("Deserialização do arquivo '{fileName}' resultou em objeto nulo.", configFileName);
                    return null;
                }

                // Validação do objeto deserializado
                var validationErrors = ValidateAppTask(appTask);
                if (validationErrors.Any())
                {
                    _logger.LogError("Arquivo '{fileName}' contém erros de configuração: {errors}", 
                        configFileName, string.Join(", ", validationErrors));
                    return null;
                }

                _logger.LogInformation("Configuração carregada com sucesso de '{fileName}': {taskName} v{version} com {folderCount} pasta(s)", 
                    configFileName, appTask.Name ?? "N/A", appTask.Version ?? "N/A", appTask.FolderMaps?.Count() ?? 0);
                
                return appTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao mapear tarefas do arquivo '{fileName}': {message}", configFileName, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Validação básica da estrutura JSON
        /// </summary>
        /// <param name="json">String JSON</param>
        /// <returns>True se a estrutura parece válida</returns>
        private bool IsValidJsonStructure(string json)
        {
            try
            {
                json = json.Trim();
                
                // Verificações básicas
                if (!json.StartsWith("{") || !json.EndsWith("}"))
                    return false;

                // Conta chaves para verificar balanceamento básico
                int openBraces = json.Count(c => c == '{');
                int closeBraces = json.Count(c => c == '}');
                
                return openBraces == closeBraces && openBraces > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Valida se o objeto AppTask está corretamente configurado
        /// </summary>
        /// <param name="appTask">Objeto a ser validado</param>
        /// <returns>Lista de erros encontrados</returns>
        private List<string> ValidateAppTask(AppTask appTask)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(appTask.Name))
                errors.Add("Nome da aplicação não pode ser nulo ou vazio");

            if (string.IsNullOrWhiteSpace(appTask.Version))
                errors.Add("Versão da aplicação não pode ser nula ou vazia");

            if (appTask.FolderMaps == null)
            {
                errors.Add("FolderMaps não pode ser nulo");
                return errors; // Para aqui se FolderMaps for null
            }

            if (!appTask.FolderMaps.Any())
                errors.Add("Pelo menos uma pasta deve ser configurada em FolderMaps");

            // Validação de cada FolderMap
            for (int i = 0; i < appTask.FolderMaps.Count(); i++)
            {
                var folderMap = appTask.FolderMaps.ElementAt(i);
                var folderErrors = ValidateFolderMap(folderMap, i);
                errors.AddRange(folderErrors);
            }

            return errors;
        }

        /// <summary>
        /// Valida uma configuração de pasta específica
        /// </summary>
        /// <param name="folderMap">Configuração da pasta</param>
        /// <param name="index">Índice da pasta na lista</param>
        /// <returns>Lista de erros encontrados</returns>
        private List<string> ValidateFolderMap(FolderMap? folderMap, int index)
        {
            var errors = new List<string>();

            if (folderMap == null)
            {
                errors.Add($"FolderMap[{index}] é nulo");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(folderMap.Name))
                errors.Add($"FolderMap[{index}]: Nome não pode ser nulo ou vazio");

            if (string.IsNullOrWhiteSpace(folderMap.FolderPathOrigin))
                errors.Add($"FolderMap[{index}]: FolderPath não pode ser nulo ou vazio");

            if (string.IsNullOrWhiteSpace(folderMap.SFTPPathDestination))
                errors.Add($"FolderMap[{index}]: SFTPPathDestination não pode ser nulo ou vazio");

            if (folderMap.TasksMaps == null)
            {
                errors.Add($"FolderMap[{index}]: TasksMaps não pode ser nulo");
            }
            else if (!folderMap.TasksMaps.Any())
            {
                errors.Add($"FolderMap[{index}]: Pelo menos uma tarefa deve ser configurada em TasksMaps");
            }

            return errors;
        }
    }
}
