using Infrastructure.Extensions;
using Models.Configurations;
using Models.MappingTasks;
using Renci.SshNet;

namespace Infrastructure.Factorys
{
    public static class CopyToDestination
    {
        /// <summary>
        /// Executa a cópia de arquivos da Origem para o Destino, pode ser um SFTP ou drive local 
        /// Utiliza os argumentos 1 e 2 da TaskActions para determinar o caminho de origem e destino
        /// </summary>
        /// <param name="taskActions"></param>
        /// <returns>Retorna uma TaskActions</returns>
        public static TaskActions Copy(this TaskActions taskActions)
        {
            if (taskActions == null)
            {
                return new TaskActions { Success = false, Message = "TaskActions não pode ser null.\n" };
            }

            if (string.IsNullOrWhiteSpace(taskActions.Argument1))
            {
                taskActions.Success = false;
                taskActions.Message = "Caminho de origem (Argument1) não pode ser nulo ou vazio.\n";
                return taskActions;
            }

            if (string.IsNullOrWhiteSpace(taskActions.Argument2))
            {
                taskActions.Success = false;
                taskActions.Message = "Caminho de destino (Argument2) não pode ser nulo ou vazio.\n";
                return taskActions;
            }

            try
            {
                var files = taskActions.Argument1.GetFiles();

                if (files == null || !files.Any())
                {
                    taskActions.Success = true;
                    taskActions.Message = "Nenhum arquivo encontrado para cópia.\n";
                    return taskActions;
                }

                // Verifica se estou copiando para um SFTP ou para outro diretório do computador
                bool isLocalDestination = IsLocalPath.Execute(taskActions.Argument2);

                if (isLocalDestination)
                {
                    return CopyToLocalDirectory(files, taskActions);
                }
                else
                {
                    return CopyToSFTP(files, taskActions);
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Diretório não encontrado: {ex.Message}\n";
                return taskActions;
            }
            catch (UnauthorizedAccessException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Acesso negado: {ex.Message}\n";
                return taskActions;
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro inesperado ao executar cópia: {ex.Message}\n";
                return taskActions;
            }
        }

        /// <summary>
        /// Copia arquivos para um diretório local
        /// </summary>
        /// <param name="files">Lista de arquivos a serem copiados</param>
        /// <param name="taskActions">Ações da tarefa</param>
        /// <returns>TaskActions com resultado da operação</returns>
        private static TaskActions CopyToLocalDirectory(IEnumerable<FileInfo>? files, TaskActions taskActions)
        {
            if (files == null)
            {
                taskActions.Success = false;
                taskActions.Message = "Lista de arquivos é null.";
                return taskActions;
            }

            var fileList = files.ToList();
            if (!fileList.Any())
            {
                taskActions.Success = true;
                taskActions.Message = "Nenhum arquivo para copiar.";
                return taskActions;
            }

            try
            {
                string destinationPath = taskActions.Argument2;

                int copiedFiles = 0;
                foreach (var file in fileList)
                {
                    if (file == null || !file.Exists)
                    {
                        taskActions.Message += $"Arquivo não existe ou é inválido: {file?.FullName ?? "null"}\r\n";
                        continue;
                    }

                    try
                    {
                        if (taskActions.ShouldInspect)
                        {
                            var content = InspectFileFactory.Inspect(file.FullName, taskActions.InspectPartOfFile);
                            //remover os zeros as esquerda se houver
                            taskActions.Inspect_VAR = content.TrimStart('0').Trim();
                            destinationPath = destinationPath.Replace("@Inspect_VAR", taskActions.Inspect_VAR);
                        }

                        // Verifica se o diretório de destino existe, se não, tenta criar
                        try
                        {
                            if (!Directory.Exists(destinationPath))
                            {
                                Directory.CreateDirectory(destinationPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            taskActions.Message += $"Aviso: Não foi possível verificar/criar diretório local: {ex.Message}\r\n";
                        }

                        string localFilePath = Path.Combine(destinationPath, file.Name);

                        File.Copy(file.FullName, localFilePath, overwrite: true);
                        taskActions.Message += $"Arquivo copiado: {file.Name}\r\n";

                        taskActions.FilesProcessed.Add(file.Name);

                        copiedFiles++;
                    }
                    catch (IOException ex)
                    {
                        taskActions.Message += $"Erro de I/O ao copiar {file.Name}: {ex.Message}\r\n";
                    }
                    catch (Exception ex)
                    {
                        taskActions.Message += $"Erro ao copiar o arquivo {file.Name}: {ex.Message}\r\n";
                    }
                }

                taskActions.Success = copiedFiles > 0;
                if (taskActions.Success)
                {
                    taskActions.Message += $"Total de {copiedFiles} arquivo(s) copiado(s) com sucesso para o diretório local.";
                }
                else
                {
                    taskActions.Message += "Nenhum arquivo foi copiado com sucesso para o diretório local.";
                }
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro ao copiar arquivos para o diretório local: {ex.Message}";
            }

            return taskActions;
        }

        /// <summary>
        /// Copia arquivos para o servidor SFTP
        /// </summary>
        /// <param name="files">Lista de arquivos a serem copiados</param>
        /// <param name="taskActions">Ações da tarefa</param>
        /// <returns>TaskActions com resultado da operação</returns>
        private static TaskActions CopyToSFTP(IEnumerable<FileInfo>? files, TaskActions taskActions)
        {
            if (files == null)
            {
                taskActions.Success = false;
                taskActions.Message = "Lista de arquivos é null.";
                return taskActions;
            }

            var fileList = files.ToList();
            if (!fileList.Any())
            {
                taskActions.Success = true;
                taskActions.Message = "Nenhum arquivo para copiar.";
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

                clientSFTP = new SftpClient(SFTPCredentials.SFTPUrl, SFTPCredentials.Port, SFTPCredentials.UsuarioSFTP, SFTPCredentials.Senha);

                // Configurar timeout
                clientSFTP.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
                clientSFTP.OperationTimeout = TimeSpan.FromMinutes(5);

                clientSFTP.Connect();

                if (!clientSFTP.IsConnected)
                {
                    taskActions.Success = false;
                    taskActions.Message = "Falha ao conectar no servidor SFTP.";
                    return taskActions;
                }

                string destinationPath = taskActions.Argument2.Replace("SFTP:", "");



                int copiedFiles = 0;
                foreach (var file in fileList)
                {
                    if (file == null || !file.Exists)
                    {
                        taskActions.Message += $"Arquivo não existe ou é inválido: {file?.FullName ?? "null"}\r\n";
                        continue;
                    }

                    FileStream? fs = null;
                    try
                    {
                        if (taskActions.ShouldInspect)
                        {
                            var content = InspectFileFactory.Inspect(file.FullName, taskActions.InspectPartOfFile);
                            //remover os zeros as esquerda se houver
                            taskActions.Inspect_VAR = content.TrimStart('0').Trim();
                            destinationPath = destinationPath.Replace("@Inspect_VAR", taskActions.Inspect_VAR);
                        }

                        // Verifica se o diretório existe no SFTP, se não, tenta criar
                        try
                        {
                            if (!clientSFTP.Exists(destinationPath))
                            {
                                // Tenta criar o diretório recursivamente
                                CreateSftpDirectory(clientSFTP, destinationPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            taskActions.Message += $"Aviso: Não foi possível verificar/criar diretório no SFTP: {ex.Message}\r\n";
                        }

                        string remoteFilePath = destinationPath + "/" + file.Name;
                        fs = File.OpenRead(file.FullName);
                        IAsyncResult? uploadResult = clientSFTP.BeginUploadFile(fs, remoteFilePath);
                        clientSFTP.EndUploadFile(uploadResult);

                        //clientSFTP.UploadFile(fs, remoteFilePath);


                        taskActions.Message += $"Arquivo copiado: {file.Name}\r\n";

                        taskActions.FilesProcessed.Add(file.Name);

                        copiedFiles++;
                    }
                    catch (IOException ex)
                    {
                        taskActions.Message += $"Erro de I/O ao copiar {file.Name}: {ex.Message}\r\n";
                    }
                    catch (Exception ex)
                    {
                        taskActions.Message += $"Erro ao fazer upload do arquivo {file.Name}: {ex.Message}\r\n";
                    }
                    finally
                    {
                        fs?.Dispose();
                    }
                }

                taskActions.Success = copiedFiles > 0;
                if (taskActions.Success)
                {
                    taskActions.Message += $"Total de {copiedFiles} arquivo(s) copiado(s) com sucesso para o SFTP.";
                }
                else
                {
                    taskActions.Message += "Nenhum arquivo foi copiado com sucesso para o SFTP.";
                }
            }
            catch (Renci.SshNet.Common.SshConnectionException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro de conexão SFTP: {ex.Message}";
            }
            catch (Renci.SshNet.Common.SshAuthenticationException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro de autenticação SFTP: {ex.Message}";
            }
            catch (TimeoutException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Timeout na conexão SFTP: {ex.Message}";
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro ao copiar para o SFTP: {ex.Message}";
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

        /// <summary>
        /// Cria um diretório no SFTP recursivamente
        /// </summary>
        private static void CreateSftpDirectory(SftpClient client, string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/" || path == "\\")
                return;

            try
            {
                if (client.Exists(path))
                    return;

                string parentDir = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? string.Empty;

                if (!string.IsNullOrEmpty(parentDir) && parentDir != "/" && parentDir != path)
                {
                    CreateSftpDirectory(client, parentDir);
                }

                client.CreateDirectory(path);
            }
            catch (Exception)
            {
                // Ignora erros de criação de diretório já que pode já existir
                // ou pode não ter permissão para criar
            }
        }
    }
}
