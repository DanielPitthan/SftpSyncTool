using Infrastructure;
using Infrastructure.Email;
using Infrastructure.Extensions;
using Infrastructure.Factorys;
using Microsoft.Extensions.Options;
using Models.Enums;
using Models.MappingTasks;

namespace CopyToSFTPObserver
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AppSettings _appSettings;
        private readonly AppTaskMapperConfigurator _appTaskMapperConfigurator;

        public Worker(ILogger<Worker> logger,
            IOptions<AppSettings> appSettings,
            AppTaskMapperConfigurator appTaskMapperConfigurator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appSettings = appSettings?.Value ?? throw new ArgumentNullException(nameof(appSettings));
            _appTaskMapperConfigurator = appTaskMapperConfigurator ?? throw new ArgumentNullException(nameof(appTaskMapperConfigurator));
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Serviço em execução: {time}", DateTimeOffset.Now);
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Serviço em parada: {time}", DateTimeOffset.Now);
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Processamento em execução: {time}", DateTimeOffset.Now);

                //Faz o mapeamento antes de entrar no loop, para não ter que mapear constantemente 
                _logger.LogInformation("Mapeando ações a serem executadas...");
                AppTask? appTask = _appTaskMapperConfigurator.MapAppTask();
                _logger.LogInformation($"Ações mapeadas: {appTask?.FolderMaps.Count() ?? 0}");

                if (appTask == null)
                {
                    _logger.LogWarning("Processo finalizado sem ações a serem executadas. Causa: Nenhuma ação mapeada ou erro ao mapear ações.");
                    await this.StopAsync(stoppingToken);
                    return; // Importante: sair do método após chamar StopAsync
                }

                if (appTask.FolderMaps == null || !appTask.FolderMaps.Any())
                {
                    _logger.LogWarning("Nenhuma pasta foi mapeada para processamento.");
                    await this.StopAsync(stoppingToken);
                    return;
                }

                _logger.LogInformation("Executando: {name} - Version: {version}", appTask.Name ?? "N/A", appTask.Version ?? "N/A");

                //Executa o processamento das pastas
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await ProcessFolders(appTask.FolderMaps, stoppingToken);
                        _logger.LogInformation("Todas as tarefas foram executadas.");
                        await ProximaExecucao(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Processamento cancelado pelo token de cancelamento.");
                        break;
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        _logger.LogError("Diretório não encontrado: {message}", ex.Message);
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Aguarda antes de tentar novamente
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogError("Acesso negado: {message}", ex.Message);
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogError("Erro de I/O: {message}", ex.Message);
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro inesperado durante o processamento: {message}", ex.Message);
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Delay maior para erros inesperados
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Erro crítico no serviço: {message}", ex.Message);
                throw; // Re-throw para garantir que o serviço seja parado
            }
        }

        private async Task ProcessFolders(IEnumerable<FolderMap> folderMaps, CancellationToken stoppingToken)
        {
            foreach (var folderMap in folderMaps)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    await ProcessSingleFolder(folderMap, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar pasta {folderName}: {message}",
                        folderMap?.Name ?? "N/A", ex.Message);
                    // Continue processando outras pastas mesmo se uma falhar
                }
            }
        }

        private async Task ProcessSingleFolder(FolderMap folderMap, CancellationToken stoppingToken)
        {
            if (folderMap == null)
            {
                _logger.LogWarning("FolderMap é null, pulando processamento.");
                return;
            }

            _logger.LogInformation("Processando pasta: {folderName}", folderMap.Name ?? "N/A");
            _logger.LogInformation("Caminho da pasta: {folderPath}", folderMap.FolderPathOrigin ?? "N/A");
            _logger.LogInformation("Destino SFTP: {sftpPathDestination}", folderMap.SFTPPathDestination ?? "N/A");
            _logger.LogInformation("Destino no caso de erro: {processedFilesOnError}", folderMap.ProcessedFilesOnError ?? "N/A");
            _logger.LogInformation("Destino no caso de sucesso: {processedFilesOnSuccess}", folderMap.ProcessedFilesOnSuccess ?? "N/A");
            _logger.LogInformation("Notificação por e-mail: {emailNotify}", folderMap.EmailNotify);

            if (folderMap.TasksMaps == null || !folderMap.TasksMaps.Any())
            {
                _logger.LogWarning("Nenhuma tarefa mapeada para a pasta {folderName}", folderMap.Name);
                return;
            }

            List<TaskActions> tasksActions = new List<TaskActions>();

            //Gera as ações a serem executadas para cada pasta
            foreach (var taskMap in folderMap.TasksMaps.OrderBy(t => t.Order))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    var variableName = taskMap.Name?.ExtractVariable();
                    var value = variableName?.GetValue(folderMap);
                    var taskName = variableName != null && !string.IsNullOrEmpty(taskMap.Name)
                        ? taskMap.Name.Replace("@" + variableName, value ?? string.Empty)
                        : taskMap.Name ?? "N/A";

                    _logger.LogInformation("[{order}] Criando tarefa: {taskName}", taskMap.Order, taskName);

                    var taskAction = TaskActionFactory.CreateTaskAction(taskMap.Task, folderMap, taskName);
                    if (taskAction != null)
                    {
                        tasksActions.Add(taskAction);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao criar tarefa {taskName}: {message}", taskMap.Name ?? "N/A", ex.Message);
                }
            }

            await ExecuteTasks(tasksActions, folderMap, stoppingToken);
        }

        private async Task ExecuteTasks(List<TaskActions> tasksActions, FolderMap folderMap, CancellationToken stoppingToken)
        {
            bool inspectOnCopy = false;
            string inspectPartOfFile = string.Empty;
            bool goToNextTask = true;

            // Cria o relatório de execução para o e-mail
            var report = new TaskExecutionReport
            {
                FolderName = folderMap.Name ?? "N/A",
                ExecutionDate = DateTime.Now
            };

            //Executa as tarefas 
            foreach (TaskActions task in tasksActions)
            {
                if (stoppingToken.IsCancellationRequested || !goToNextTask)
                    break;

                if (task == null)
                {
                    _logger.LogWarning("TaskAction é null, pulando execução.");
                    continue;
                }

                _logger.LogInformation("Executando tarefa: {taskname}", task.Name ?? "N/A");

                try
                {
                    //Identifica se há um atarefa de inspeção
                    if (task.Action == TypeOfTasks.inspect && goToNextTask)
                    {
                        inspectOnCopy = true;
                        inspectPartOfFile = task.Argument1 ?? string.Empty;
                    }


                    //Faz a cópia para o SFTP
                    if (task.Action == TypeOfTasks.copy && goToNextTask)
                    {
                        task.ShouldInspect = inspectOnCopy;
                        task.InspectPartOfFile = inspectPartOfFile;

                        TaskActions? result = task.ExecuteCopy();
                        goToNextTask = result?.Success ?? false;

                        // Adiciona arquivos processados ao relatório
                        if (result?.FilesProcessed != null)
                        {
                            report.FilesProcessed.AddRange(result.FilesProcessed);
                        }

                        // Adiciona detalhe de execução
                        report.ExecutionDetails.Add(new TaskExecutionDetail
                        {
                            TaskType = "Cópia para SFTP",
                            Message = result?.Message ?? "Resultado da cópia não disponível",
                            Success = result?.Success ?? false,
                            Timestamp = DateTime.Now
                        });

                        _logger.LogInformation(result?.Message ?? "Resultado da cópia não disponível");
                    }

                    if (!goToNextTask)
                    {
                        await HandleTaskFailure(task, folderMap);
                        continue;
                    }

                    //Faz a verificação de arquivos no SFTP
                    if (task.Action == TypeOfTasks.check && goToNextTask)
                    {
                        task.ShouldInspect = inspectOnCopy;
                        task.InspectPartOfFile = inspectPartOfFile;

                        var result = task.Check();
                        goToNextTask = true;

                        // Adiciona arquivos com erro ao relatório
                        if (result?.FilesProcessedWitError != null)
                        {
                            report.FilesWithError.AddRange(result.FilesProcessedWitError);
                        }

                        // Adiciona detalhe de execução
                        report.ExecutionDetails.Add(new TaskExecutionDetail
                        {
                            TaskType = "Verificação no SFTP",
                            Message = result?.Message ?? "Resultado da verificação não disponível",
                            Success = result?.Success ?? false,
                            Timestamp = DateTime.Now
                        });

                        _logger.LogInformation(result?.Message ?? "Resultado da verificação não disponível");
                    }

                    if (!goToNextTask)
                    {
                        await HandleTaskFailure(task, folderMap);
                        continue;
                    }

                    //Move os arquivos para a pasta de sucesso
                    if (task.Action == TypeOfTasks.move && goToNextTask)
                    {
                        var result = task.Move();
                        goToNextTask = result?.Success ?? false;

                        // Adiciona detalhe de execução
                        report.ExecutionDetails.Add(new TaskExecutionDetail
                        {
                            TaskType = "Movimentação de arquivos",
                            Message = result?.Message ?? "Resultado da movimentação não disponível",
                            Success = result?.Success ?? false,
                            Timestamp = DateTime.Now
                        });

                        _logger.LogInformation(result?.Message ?? "Resultado da movimentação não disponível");
                    }

                    if (!goToNextTask)
                    {
                        await HandleTaskFailure(task, folderMap);
                        continue;
                    }

                    //Exclui os arquivos que foram copiados
                    if (task.Action == TypeOfTasks.delete && goToNextTask)
                    {
                        var result = DeleteFileFactory.Execute(task, folderMap);
                        goToNextTask = result?.Success ?? false;

                        // Adiciona detalhe de execução
                        report.ExecutionDetails.Add(new TaskExecutionDetail
                        {
                            TaskType = "Exclusão de arquivos",
                            Message = result?.Message ?? "Resultado da exclusão não disponível",
                            Success = result?.Success ?? false,
                            Timestamp = DateTime.Now
                        });

                        _logger.LogInformation(result?.Message ?? "Resultado da exclusão não disponível");
                    }

                    if (!goToNextTask)
                    {
                        await HandleTaskFailure(task, folderMap);
                        continue;
                    }


                    if (task.Action == TypeOfTasks.notify && goToNextTask && report.HasProcessedFiles)
                    {
                        // Usa o builder para gerar o HTML do e-mail
                        var emailBuilder = new EmailNotificationBuilder(report);
                        string emailHtml = emailBuilder.BuildHtmlBody();

                        Email email = new Email(
                            folderMap.EmailNotify,
                            $"Notificação de tarefa concluída: {folderMap.Name}",
                            emailHtml
                        );

                        email.Send();
                        _logger.LogInformation($"Notificação enviada para: {folderMap.EmailNotify}");
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro inesperado ao executar tarefa {taskName}: {message}", task.Name ?? "N/A", ex.Message);
                    goToNextTask = false;
                    await HandleTaskFailure(task, folderMap);
                }
            }
        }

        private async Task HandleTaskFailure(TaskActions task, FolderMap folderMap)
        {
            try
            {
                var result = task.MoveToErrorFolder(folderMap?.ProcessedFilesOnError ?? string.Empty);
                _logger.LogError("A tarefa {taskName} falhou, não prosseguindo para a próxima tarefa.", task?.Action.ToString() ?? "N/A");
                _logger.LogError(result?.Message ?? "Erro ao mover arquivos para pasta de erro");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao mover arquivos para pasta de erro: {message}", ex.Message);
            }

            // Pequeno delay para evitar saturação em caso de erros contínuos
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        private async Task ProximaExecucao(CancellationToken stoppingToken)
        {
            var proximaExecucao = DateTime.Now.AddMilliseconds(_appSettings.IntervaloEntreExecucoes);
            _logger.LogInformation("Próxima execução em: {proximaExecucao}", proximaExecucao);

            try
            {
                await Task.Delay(_appSettings.IntervaloEntreExecucoes, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Delay cancelado pelo token de cancelamento.");
            }
        }
    }
}
