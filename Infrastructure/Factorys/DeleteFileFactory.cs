using Infrastructure.Extensions;
using Models.MappingTasks;

namespace Infrastructure.Factorys
{
    public static class DeleteFileFactory
    {
        /// <summary>
        /// Executa a exclusão de arquivos
        /// </summary>
        /// <param name="taskActions">Ação da tarefa</param>
        /// <param name="folderMap">Mapeamento da pasta (usado como fallback se taskActions.Argument1 estiver vazio)</param>
        /// <returns>TaskActions com resultado da operação</returns>
        public static TaskActions Execute(TaskActions taskActions, FolderMap folderMap)
        {
            if (taskActions == null)
            {
                return new TaskActions { Success = false, Message = "TaskActions não pode ser null." };
            }

            try
            {
                IEnumerable<FileInfo>? files = null;

                // Tenta usar o Argument1 primeiro, se não tiver usa o folderMap como fallback
                if (!string.IsNullOrWhiteSpace(taskActions.Argument1))
                {
                    try
                    {
                        files = taskActions.Argument1.GetFiles();
                    }
                    catch (Exception ex)
                    {
                        taskActions.Message += $"Erro ao obter arquivos de Argument1: {ex.Message}\r\n";
                    }
                }

                // Se não conseguiu obter arquivos do Argument1, tenta usar folderMap
                if (files == null && folderMap != null)
                {
                    try
                    {
                        files = folderMap.GetFiles();
                        taskActions.Message += "Usando folderMap como origem dos arquivos.\r\n";
                    }
                    catch (Exception ex)
                    {
                        taskActions.Success = false;
                        taskActions.Message = $"Erro ao obter arquivos do folderMap: {ex.Message}";
                        return taskActions;
                    }
                }

                if (files == null)
                {
                    taskActions.Success = false;
                    taskActions.Message = "Não foi possível obter lista de arquivos para exclusão.";
                    return taskActions;
                }

                var fileList = files.ToList();
                if (!fileList.Any())
                {
                    taskActions.Success = true;
                    taskActions.Message = "Nenhum arquivo encontrado para exclusão.";
                    return taskActions;
                }

                int deletedFiles = 0;
                int totalFiles = fileList.Count;

                foreach (var file in fileList)
                {
                    if (file == null)
                    {
                        taskActions.Message += "Arquivo null encontrado na lista, pulando.\r\n";
                        continue;
                    }

                    try
                    {
                        // Faz a verificação se o arquivo existe, se não existir não faz nada
                        if (!File.Exists(file.FullName))
                        {   
                            taskActions.Message += $"Arquivo {file.Name} não encontrado para exclusão.\r\n";
                            continue; // Pula para o próximo arquivo
                        }

                        // Verifica se o arquivo não está sendo usado por outro processo
                        if (IsFileLocked(file))
                        {
                            taskActions.Message += $"Arquivo {file.Name} está sendo usado por outro processo, pulando exclusão.\r\n";
                            continue;
                        }

                        // Exclui o arquivo
                        File.Delete(file.FullName);
                        taskActions.Message += $"Arquivo {file.Name} excluído com sucesso.\r\n";
                        deletedFiles++;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        taskActions.Message += $"Acesso negado ao excluir arquivo {file.Name}: {ex.Message}\r\n";
                    }
                    catch (IOException ex)
                    {
                        taskActions.Message += $"Erro de I/O ao excluir arquivo {file.Name}: {ex.Message}\r\n";
                    }
                    catch (Exception ex)
                    {
                        taskActions.Message += $"Erro inesperado ao excluir arquivo {file.Name}: {ex.Message}\r\n";
                    }
                }

                // Considera sucesso se excluiu pelo menos um arquivo ou se não havia arquivos
                taskActions.Success = deletedFiles > 0 || totalFiles == 0;
                
                if (deletedFiles > 0)
                {
                    taskActions.Message += $"Total de {deletedFiles} de {totalFiles} arquivo(s) excluído(s) com sucesso.";
                }
                else if (totalFiles > 0)
                {
                    taskActions.Message += "Nenhum arquivo foi excluído com sucesso.";
                    taskActions.Success = false;
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Diretório não encontrado: {ex.Message}";
            }
            catch (UnauthorizedAccessException ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Acesso negado: {ex.Message}";
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro inesperado durante exclusão de arquivos: {ex.Message}";
            }

            return taskActions;
        }

        /// <summary>
        /// Verifica se um arquivo está sendo usado por outro processo
        /// </summary>
        /// <param name="file">Arquivo a ser verificado</param>
        /// <returns>True se o arquivo estiver bloqueado</returns>
        private static bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                // Arquivo está sendo usado
                return true;
            }
            catch (Exception)
            {
                // Outros erros também indicam que não pode acessar o arquivo
                return true;
            }

            return false;
        }
    }
}
