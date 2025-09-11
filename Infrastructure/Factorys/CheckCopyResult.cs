using Infrastructure.Extensions;
using Models.Configurations;
using Models.MappingTasks;
using Renci.SshNet;

namespace Infrastructure.Factorys
{
    public static class CheckCopyResult
    {
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
        /// Verifica se os arquivos existem no diretório local
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
                        continue;
                    }

                    string destinationFile = Path.Combine(taskActions.Argument2, file.Name);
                    bool exists = File.Exists(destinationFile);
                    
                    if (!exists)
                    {
                        filesNotFound.Add(file.FullName);
                        results.Add(false);
                    }
                    else
                    {
                        // Verifica também se o tamanho é o mesmo
                        try
                        {
                            var destFileInfo = new FileInfo(destinationFile);
                            if (file.Length != destFileInfo.Length)
                            {
                                filesSizesDifferent.Add($"{file.Name} (origem: {file.Length} bytes, destino: {destFileInfo.Length} bytes)");
                                results.Add(false);
                            }
                            else
                            {
                                results.Add(true);
                            }
                        }
                        catch (Exception ex)
                        {
                            taskActions.Message += $"Erro ao verificar tamanho do arquivo {file.Name}: {ex.Message}\r\n";
                            results.Add(false);
                        }
                    }
                }

                taskActions.Success = results.All(r => r == true);
                
                if (taskActions.Success)
                {
                    taskActions.Message = $"Todos os {fileList.Count} arquivo(s) existem no diretório local com tamanho correto.";
                }
                else
                {
                    taskActions.Message = "Verificação falhou:\r\n";
                    if (filesNotFound.Any())
                    {
                        taskActions.Message += $"Arquivos não encontrados: {string.Join(", ", filesNotFound)}\r\n";
                    }
                    if (filesSizesDifferent.Any())
                    {
                        taskActions.Message += $"Arquivos com tamanho diferente: {string.Join(", ", filesSizesDifferent)}";
                    }
                }
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro ao verificar arquivos no diretório local: {ex.Message}";
            }

            return taskActions;
        }

        /// <summary>
        /// Verifica se os arquivos existem no servidor SFTP
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
                        continue;
                    }

                    try
                    {
                        string remoteFilePath = destinationPath + "/" + file.Name;

                        if (taskActions.ShouldInspect)
                        {
                            var content = InspectFileFactory.Inspect(file.FullName, taskActions.InspectPartOfFile);
                            //remover os zeros as esquerda se houver
                            taskActions.Inspect_VAR = content.TrimStart('0').Trim();
                            remoteFilePath = destinationPath.Replace("@Inspect_VAR", taskActions.Inspect_VAR);
                        }

                        var exists = clientSFTP.Exists(remoteFilePath);
                        
                        if (!exists)
                        {
                            filesNotFound.Add(file.FullName);
                            results.Add(false);
                        }
                       
                    }
                    catch (Exception ex)
                    {
                        taskActions.Message += $"Erro ao verificar arquivo {file.Name} no SFTP: {ex.Message}\r\n";
                        results.Add(false);
                    }
                }

                taskActions.Success = results.All(r => r == true);
                
                if (taskActions.Success)
                {
                    taskActions.Message += $"Todos os {fileList.Count} arquivo(s) foram verificados com sucesso no SFTP.";
                }
                else
                {
                    taskActions.Message += "Verificação SFTP falhou:\r\n";
                    if (filesNotFound.Any())
                    {
                        taskActions.Message += $"Arquivos não encontrados: {string.Join(", ", filesNotFound)}\r\n";
                    }
                    if (filesSizesDifferent.Any())
                    {
                        taskActions.Message += $"Arquivos com tamanho diferente: {string.Join(", ", filesSizesDifferent)}";
                    }
                }
            }
            catch (Renci.SshNet.Common.SshConnectionException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro de conexão SFTP durante verificação: {ex.Message}";
            }
            catch (Renci.SshNet.Common.SshAuthenticationException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro de autenticação SFTP durante verificação: {ex.Message}";
            }
            catch (TimeoutException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Timeout na conexão SFTP durante verificação: {ex.Message}";
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro ao verificar arquivos no SFTP: {ex.Message}";
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
                    taskActions.Message += $"\r\nAviso: Erro ao fechar conexão SFTP: {ex.Message}";
                }
            }

            return taskActions;
        }
    }
}
