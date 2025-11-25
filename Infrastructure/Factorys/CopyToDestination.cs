using Infrastructure.Extensions;
using Models.Configurations;
using Models.MappingTasks;
using Renci.SshNet;
using System.Collections.Concurrent;

namespace Infrastructure.Factorys
{
    public static class CopyToDestination
    {
        // PROTEÇÃO CONTRA CONCORRÊNCIA: Semáforo por arquivo para evitar race conditions
        // Garante que cada arquivo seja processado exclusivamente por apenas uma thread por vez
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = 
            new ConcurrentDictionary<string, SemaphoreSlim>();

        // RETRY CONFIGURATION: Constantes para política de retry automático
        private const int MaxRetries = 3;
        private const int InitialDelayMs = 500;

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
        /// Copia arquivos para um diretório local com proteção contra concorrência
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
                int copiedFiles = 0;
                int failedFiles = 0;

                foreach (var file in fileList)
                {
                    if (file == null || !file.Exists)
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Arquivo não existe ou é inválido: {file?.FullName ?? "null"}\r\n";
                        failedFiles++;
                        continue;
                    }

                    // PROTEÇÃO: Obter ou criar um semáforo único para este arquivo
                    // Isto garante que múltiplas threads não corrupam dados do mesmo arquivo
                    var fileLock = _fileLocks.GetOrAdd(file.FullName, _ => new SemaphoreSlim(1, 1));

                    try
                    {
                        // EXCLUSÃO: Adquirir lock exclusivo para este arquivo
                        fileLock.Wait();

                        try
                        {
                            // ISOLAMENTO: Contexto isolado por arquivo para evitar race condition em Inspect_VAR
                            // Cada arquivo tem seu próprio escopo de variáveis, não compartilhadas
                            string inspectVar = string.Empty;
                            string destinationPath = taskActions.Argument2;

                            if (taskActions.ShouldInspect)
                            {
                                var content = InspectFileFactory.Inspect(file.FullName, taskActions.InspectPartOfFile);
                                // Remover os zeros à esquerda se houver
                                inspectVar = content.TrimStart('0').Trim();
                                taskActions.Inspect_VAR = inspectVar;
                                destinationPath = destinationPath.Replace("@Inspect_VAR", inspectVar);
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INSPECT] Arquivo: {file.Name} | Valor extraído: {inspectVar}\r\n";
                            }

                            // Verifica se o diretório de destino existe, se não, tenta criar
                            try
                            {
                                if (!Directory.Exists(destinationPath))
                                {
                                    Directory.CreateDirectory(destinationPath);
                                    taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Diretório criado: {destinationPath}\r\n";
                                }
                            }
                            catch (Exception ex)
                            {
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [AVISO] Não foi possível verificar/criar diretório local: {ex.Message}\r\n";
                            }

                            string localFilePath = Path.Combine(destinationPath, file.Name);

                            File.Copy(file.FullName, localFilePath, overwrite: true);
                            taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [SUCESSO] Arquivo copiado para: {localFilePath}\r\n";

                            taskActions.FilesProcessed.Add(file.Name);
                            copiedFiles++;
                        }
                        finally
                        {
                            // LIBERAÇÃO: Liberar o lock para este arquivo
                            fileLock.Release();
                        }
                    }
                    catch (IOException ex)
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] I/O ao copiar {file.Name}: {ex.Message}\r\n";
                        failedFiles++;
                    }
                    catch (Exception ex)
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Erro ao copiar o arquivo {file.Name}: {ex.Message}\r\n";
                        failedFiles++;
                    }
                }

                taskActions.Success = copiedFiles > 0;
                if (taskActions.Success)
                {
                    taskActions.Message += $"\r\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RESUMO] Total de {copiedFiles} arquivo(s) copiado(s) com sucesso para o diretório local. Falhas: {failedFiles}";
                }
                else
                {
                    taskActions.Message += $"\r\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RESUMO] Nenhum arquivo foi copiado com sucesso para o diretório local. Falhas: {failedFiles}";
                }
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO CRÍTICO] Erro ao copiar arquivos para o diretório local: {ex.Message}";
            }

            return taskActions;
        }

        /// <summary>
        /// Copia arquivos para o servidor SFTP com proteção contra concorrência e retry automático
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

                int copiedFiles = 0;
                int failedFiles = 0;

                foreach (var file in fileList)
                {
                    if (file == null || !file.Exists)
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Arquivo não existe ou é inválido: {file?.FullName ?? "null"}\r\n";
                        failedFiles++;
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
                            string destinationPath = taskActions.Argument2.Replace("SFTP:", "");

                            if (taskActions.ShouldInspect)
                            {
                                var content = InspectFileFactory.Inspect(file.FullName, taskActions.InspectPartOfFile);
                                // Remover os zeros à esquerda se houver
                                inspectVar = content.TrimStart('0').Trim();
                                taskActions.Inspect_VAR = inspectVar;
                                destinationPath = destinationPath.Replace("@Inspect_VAR", inspectVar);
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INSPECT] Arquivo: {file.Name} | Valor extraído: {inspectVar}\r\n";
                            }

                            // Verifica se o diretório existe no SFTP, se não, tenta criar
                            try
                            {
                                if (!clientSFTP.Exists(destinationPath))
                                {
                                    // Tenta criar o diretório recursivamente
                                    CreateSftpDirectory(clientSFTP, destinationPath);
                                    taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Diretório criado no SFTP: {destinationPath}\r\n";
                                }
                            }
                            catch (Exception ex)
                            {
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [AVISO] Não foi possível verificar/criar diretório no SFTP: {ex.Message}\r\n";
                            }

                            string remoteFilePath = destinationPath + "/" + file.Name;

                            // RETRY: Executar upload com retry automático e backoff exponencial
                            bool uploadSuccess = ExecuteSftpUploadWithRetry(clientSFTP, file, remoteFilePath, taskActions);

                            if (uploadSuccess)
                            {
                                taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [SUCESSO] Arquivo enviado para SFTP: {remoteFilePath}\r\n";
                                taskActions.FilesProcessed.Add(file.Name);
                                copiedFiles++;
                            }
                            else
                            {
                                failedFiles++;
                            }
                        }
                        finally
                        {
                            // LIBERAÇÃO: Liberar o lock para este arquivo
                            fileLock.Release();
                        }
                    }
                    catch (IOException ex)
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] I/O ao copiar {file.Name}: {ex.Message}\r\n";
                        failedFiles++;
                    }
                    catch (Exception ex)
                    {
                        taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Erro ao fazer upload do arquivo {file.Name}: {ex.Message}\r\n";
                        failedFiles++;
                    }
                }

                taskActions.Success = copiedFiles > 0;
                if (taskActions.Success)
                {
                    taskActions.Message += $"\r\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RESUMO] Total de {copiedFiles} arquivo(s) copiado(s) com sucesso para o SFTP. Falhas: {failedFiles}";
                }
                else
                {
                    taskActions.Message += $"\r\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RESUMO] Nenhum arquivo foi copiado com sucesso para o SFTP. Falhas: {failedFiles}";
                }
            }
            catch (Renci.SshNet.Common.SshConnectionException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Erro de conexão SFTP: {ex.Message}";
            }
            catch (Renci.SshNet.Common.SshAuthenticationException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Erro de autenticação SFTP: {ex.Message}";
            }
            catch (TimeoutException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Timeout na conexão SFTP: {ex.Message}";
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO CRÍTICO] Erro ao copiar para o SFTP: {ex.Message}";
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

        /// <summary>
        /// Executa o upload de arquivo no SFTP com retry automático e backoff exponencial.
        /// PROTEÇÃO: Falhas temporárias (timeout, I/O, SSH) são retentadas com delay crescente
        /// ISOLAMENTO: Cada tentativa de retry abre um novo FileStream, evitando state corruption
        /// </summary>
        private static bool ExecuteSftpUploadWithRetry(SftpClient client, FileInfo file, string remoteFilePath, TaskActions taskActions)
        {
            int retryCount = 0;
            int delayMs = InitialDelayMs;

            while (retryCount < MaxRetries)
            {
                FileStream? fs = null;
                try
                {
                    fs = File.OpenRead(file.FullName);
                    IAsyncResult? uploadResult = client.BeginUploadFile(fs, remoteFilePath);
                    client.EndUploadFile(uploadResult);
                    return true;
                }
                catch (IOException ex) when (retryCount < MaxRetries - 1)
                {
                    // IOException pode ser temporária (ex: arquivo em uso), registrar e tentar novamente
                    retryCount++;
                    taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RETRY {retryCount}/{MaxRetries}] I/O error em {file.Name}, aguardando {delayMs}ms antes de tentar novamente: {ex.Message}\r\n";
                    System.Threading.Thread.Sleep(delayMs);
                    delayMs *= 2; // Backoff exponencial: 500ms, 1s, 2s
                }
                catch (TimeoutException ex) when (retryCount < MaxRetries - 1)
                {
                    // Timeout pode ser temporário (latência de rede), registrar e tentar novamente
                    retryCount++;
                    taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RETRY {retryCount}/{MaxRetries}] Timeout ao enviar {file.Name}, aguardando {delayMs}ms antes de tentar novamente: {ex.Message}\r\n";
                    System.Threading.Thread.Sleep(delayMs);
                    delayMs *= 2; // Backoff exponencial
                }
                catch (Renci.SshNet.Common.SshException ex) when (retryCount < MaxRetries - 1)
                {
                    // SshException pode ser temporária (ex: connection reset, transient network error)
                    retryCount++;
                    taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RETRY {retryCount}/{MaxRetries}] Erro SSH em {file.Name}, aguardando {delayMs}ms antes de tentar novamente: {ex.Message}\r\n";
                    System.Threading.Thread.Sleep(delayMs);
                    delayMs *= 2; // Backoff exponencial
                }
                catch (Exception ex)
                {
                    // Outros erros não são retentáveis
                    taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO NÃO-RETENTÁVEL] Falha irrecuperável ao enviar {file.Name}: {ex.GetType().Name} - {ex.Message}\r\n";
                    return false;
                }
                finally
                {
                    fs?.Dispose();
                }
            }

            taskActions.Message += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERRO] Falha ao enviar {file.Name} após {MaxRetries} tentativas. Limite de retries atingido.\r\n";
            return false;
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
