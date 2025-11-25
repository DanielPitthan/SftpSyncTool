using Infrastructure.Extensions;
using Models.Configurations;
using Models.MappingTasks;
using Renci.SshNet;
using System.Collections.Concurrent;

namespace Infrastructure.Factorys
{
    public static class CheckCopyResult
    {
        // PROTEÇÃO CONTRA CONCORRÊNCIA: Semáforo por arquivo para evitar race conditions
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = 
            new ConcurrentDictionary<string, SemaphoreSlim>();

        /// <summary>
        /// Faz uma verificação se os arquivos entre origem e destino 
        /// Utiliza os argumentos 1 e 2 da TaskActions para determinar o caminho de origem e destino
        /// </summary>
        /// <param name="taskActions"></param>
        /// <returns>Retorna uma TaskActions</returns>
        public static TaskActions Execute(TaskActions taskActions)
        {
            if (taskActions == null)
            {
                return new TaskActions { Success = false, Message = "TaskActions não pode ser null." };
            }

            if (string.IsNullOrWhiteSpace(taskActions.Argument1))
            {
                taskActions.Success = false;
                taskActions.Message = "Caminho de origem (Argument1) não pode ser nulo ou vazio.";
                return taskActions;
            }

            if (string.IsNullOrWhiteSpace(taskActions.Argument2))
            {
                taskActions.Success = false;
                taskActions.Message = "Caminho de destino (Argument2) não pode ser nulo ou vazio.";
                return taskActions;
            }

            try
            {
                var files = taskActions.Argument1.GetFiles();

                if (files == null || !files.Any())
                {
                    taskActions.Success = true;
                    taskActions.Message = "Nenhum arquivo encontrado para verificação.";
                    return taskActions;
                }

                // Verifica se estou verificando arquivos em SFTP ou em diretório local
                bool isLocalDestination = IsLocalPath.Execute(taskActions.Argument2);

                if (isLocalDestination)
                {
                    return CheckLocalDirectory(files, taskActions);
                }
                else
                {
                    return CheckSFTP(files, taskActions);
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Diretório não encontrado: {ex.Message}";
                return taskActions;
            }
            catch (UnauthorizedAccessException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Acesso negado: {ex.Message}";
                return taskActions;
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro inesperado durante verificação: {ex.Message}";
                return taskActions;
            }
        }

        /// <summary>
        /// Verifica se os arquivos existem no diretório local com proteção contra concorrência
        /// </summary>
        /// <param name="files">Lista de arquivos a serem verificados</param>
        /// <param name="taskActions">Ações da tarefa</param>
        /// <returns>TaskActions com resultado da verificação</returns>
        private static TaskActions CheckLocalDirectory(IEnumerable<FileInfo>? files, TaskActions taskActions)
        {
            if (files == null)
            {
                taskActions.Success = false;
                taskActions.Message = "Lista de arquivos é null.";
                return taskActions;
            }

            try
            {
                var fileList = files.ToList();
                if (!fileList.Any())
                {
                    taskActions.Success = true;
                    taskActions.Message = "Nenhum arquivo para verificar.";
                    return taskActions;
                }

                List<bool> results = new List<bool>();
                List<string> filesNotFound = new List<string>();
                List<string> filesSizesDifferent = new List<string>();

                foreach (var file in fileList)
                {
                    if (file == null || !file.Exists)
                    {
                        filesNotFound.Add(file?.FullName ?? "arquivo null");
                        results.Add(false);
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Arquivo de origem não existe: {file?.FullName ?? "null"}\r\n";
                        continue;
                    }

                    // PROTEÇÃO: Obter ou criar um semáforo único para este arquivo
                    var fileLock = _fileLocks.GetOrAdd(file.FullName, _ => new SemaphoreSlim(1, 1));

                    try
                    {
                        // EXCLUSÃO: Adquirir lock exclusivo para este arquivo
                        fileLock.Wait();

                        try
                        {
                            // ISOLAMENTO: Contexto isolado por arquivo para evitar race condition em Inspect_VAR
                            string inspectVar = string.Empty;
                            string localFilePath = taskActions.Argument2;

                            if (taskActions.ShouldInspect)
                            {
                                var content = InspectFileFactory.Inspect(file.FullName, taskActions.InspectPartOfFile);
                                // Remover os zeros à esquerda se houver
                                inspectVar = content.TrimStart('0').Trim();
                                taskActions.Inspect_VAR = inspectVar;
                                localFilePath = taskActions.Argument2.Replace("@Inspect_VAR", inspectVar);
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INSPECT] Arquivo: {file.Name} | Valor extraído: {inspectVar}\r\n";
                            }

                            string destinationFile = Path.Combine(localFilePath, file.Name);
                            var exists = File.Exists(destinationFile);
                            results.Add(exists);
                            
                            if (!exists)
                            {
                                filesNotFound.Add(file?.FullName ?? "arquivo null");
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [NÃO-ENCONTRADO] {file.Name} não existe em: {destinationFile}\r\n";
                            }
                            else
                            {
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [VERIFICADO] {file.Name} existe em: {destinationFile}\r\n";
                            }
                        }
                        finally
                        {
                            // LIBERAÇÃO: Liberar o lock para este arquivo
                            fileLock.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Erro ao verificar arquivo {file.Name} no diretório local: {ex.Message}\r\n";
                        results.Add(false);
                    }
                }

                taskActions.FilesProcessedWitError = filesNotFound;

                taskActions.Success = results.All(r => r == true);
                
                if (taskActions.Success)
                {
                    taskActions.Message += $"\r\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RESUMO] Todos os {fileList.Count} arquivo(s) foram verificados com sucesso no diretório local.";
                }
                else
                {
                    taskActions.Message += "\r\nVerificação local falhou:\r\n";
                    if (filesNotFound.Any())
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RESUMO] Arquivos não encontrados ({filesNotFound.Count}): {string.Join(", ", filesNotFound)}\r\n";
                    }
                    if (filesSizesDifferent.Any())
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RESUMO] Arquivos com tamanho diferente ({filesSizesDifferent.Count}): {string.Join(", ", filesSizesDifferent)}";
                    }
                }
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO CRÍTICO] Erro ao verificar arquivos no diretório local: {ex.Message}";
            }

            return taskActions;
        }

        /// <summary>
        /// Verifica se os arquivos existem no servidor SFTP com proteção contra concorrência
        /// </summary>
        /// <param name="files">Lista de arquivos a serem verificados</param>
        /// <param name="taskActions">Ações da tarefa</param>
        /// <returns>TaskActions com resultado da verificação</returns>
        private static TaskActions CheckSFTP(IEnumerable<FileInfo>? files, TaskActions taskActions)
        {
            if (files == null)
            {
                taskActions.Success = false;
                taskActions.Message = "Lista de arquivos é null.";
                return taskActions;
            }

            SftpClient? clientSFTP = null;
            try
            {
                // Validação das credenciais SFTP
                if (string.IsNullOrEmpty(SFTPCredentials.SFTPUrl))
                {
                    taskActions.Success = false;
                    taskActions.Message = "URL do SFTP não configurada.";
                    return taskActions;
                }

                if (string.IsNullOrEmpty(SFTPCredentials.UsuarioSFTP))
                {
                    taskActions.Success = false;
                    taskActions.Message = "Usuário SFTP não configurado.";
                    return taskActions;
                }

                var fileList = files.ToList();
                if (!fileList.Any())
                {
                    taskActions.Success = true;
                    taskActions.Message = "Nenhum arquivo para verificar.";
                    return taskActions;
                }

                clientSFTP = new SftpClient(SFTPCredentials.SFTPUrl, SFTPCredentials.Port, SFTPCredentials.UsuarioSFTP, SFTPCredentials.Senha);
                
                // Configurar timeout
                clientSFTP.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
                clientSFTP.OperationTimeout = TimeSpan.FromMinutes(2);
                
                clientSFTP.Connect();

                if (!clientSFTP.IsConnected)
                {
                    taskActions.Success = false;
                    taskActions.Message = "Falha ao conectar no servidor SFTP para verificação.";
                    return taskActions;
                }

                List<bool> results = new List<bool>();
                List<string> filesNotFound = new List<string>();
                List<string> filesSizesDifferent = new List<string>();

                string destinationPath = taskActions.Argument2.Replace("SFTP:", "");

                foreach (var file in fileList)
                {
                    if (file == null || !file.Exists)
                    {
                        filesNotFound.Add(file?.FullName ?? "arquivo null");
                        results.Add(false);
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Arquivo de origem não existe: {file?.FullName ?? "null"}\r\n";
                        continue;
                    }

                    // PROTEÇÃO: Obter ou criar um semáforo único para este arquivo
                    var fileLock = _fileLocks.GetOrAdd(file.FullName, _ => new SemaphoreSlim(1, 1));

                    try
                    {
                        // EXCLUSÃO: Adquirir lock exclusivo para este arquivo
                        fileLock.Wait();

                        try
                        {
                            // ISOLAMENTO: Contexto isolado por arquivo para evitar race condition em Inspect_VAR
                            string inspectVar = string.Empty;
                            string remoteDestinationPath = destinationPath;

                            if (taskActions.ShouldInspect)
                            {
                                var content = InspectFileFactory.Inspect(file.FullName, taskActions.InspectPartOfFile);
                                // Remover os zeros à esquerda se houver
                                inspectVar = content.TrimStart('0').Trim();
                                taskActions.Inspect_VAR = inspectVar;
                                remoteDestinationPath = destinationPath.Replace("@Inspect_VAR", inspectVar);
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INSPECT] Arquivo: {file.Name} | Valor extraído: {inspectVar}\r\n";
                            }
                            
                            string destinationFile = remoteDestinationPath + "/" + file.Name;
                            var exists = clientSFTP.Exists(destinationFile);
                            results.Add(exists);                        
                            
                            if (!exists)
                            {
                                filesNotFound.Add(file?.FullName ?? "arquivo null");
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [NÃO-ENCONTRADO] {file.Name} não existe em: {destinationFile}\r\n";
                            }
                            else
                            {
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [VERIFICADO] {file.Name} existe em: {destinationFile}\r\n";
                            }
                        }
                        finally
                        {
                            // LIBERAÇÃO: Liberar o lock para este arquivo
                            fileLock.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Erro ao verificar arquivo {file.Name} no SFTP: {ex.Message}\r\n";
                        results.Add(false);
                    }
                }

                taskActions.FilesProcessedWitError = filesNotFound;

                taskActions.Success = results.All(r => r == true);
                
                if (taskActions.Success)
                {
                    taskActions.Message += $"\r\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RESUMO] Todos os {fileList.Count} arquivo(s) foram verificados com sucesso no SFTP.";
                }
                else
                {
                    taskActions.Message += "\r\nVerificação SFTP falhou:\r\n";
                    if (filesNotFound.Any())
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RESUMO] Arquivos não encontrados ({filesNotFound.Count}): {string.Join(", ", filesNotFound)}\r\n";
                    }
                    if (filesSizesDifferent.Any())
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RESUMO] Arquivos com tamanho diferente ({filesSizesDifferent.Count}): {string.Join(", ", filesSizesDifferent)}";
                    }
                }
            }
            catch (Renci.SshNet.Common.SshConnectionException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Erro de conexão SFTP durante verificação: {ex.Message}";
            }
            catch (Renci.SshNet.Common.SshAuthenticationException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Erro de autenticação SFTP durante verificação: {ex.Message}";
            }
            catch (TimeoutException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Timeout na conexão SFTP durante verificação: {ex.Message}";
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO CRÍTICO] Erro ao verificar arquivos no SFTP: {ex.Message}";
            }
            finally
            {
                try
                {
                    clientSFTP?.Disconnect();
                    clientSFTP?.Dispose();
                }
                catch (Exception ex)
                {
                    // Log mas não falha a operação por isso
                    taskActions.Message += $"\r\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [AVISO] Erro ao fechar conexão SFTP: {ex.Message}";
                }
            }

            return taskActions;
        }
    }
}
