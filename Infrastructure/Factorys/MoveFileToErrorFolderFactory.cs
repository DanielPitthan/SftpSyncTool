using Infrastructure.Extensions;
using Models.MappingTasks;

namespace Infrastructure.Factorys
{
    public static class MoveFileToErrorFolderFactory
    {
        /// <summary>
        /// Move arquivos para a pasta de erro
        /// </summary>
        /// <param name="taskActions">Ação da tarefa</param>
        /// <param name="processedFilesOnError">Caminho da pasta de erro</param>
        /// <returns>TaskActions com resultado da operação</returns>
        public static TaskActions Execute(TaskActions taskActions, string processedFilesOnError)
        {
            if (taskActions == null)
            {
                return new TaskActions { Success = false, Message = "TaskActions não pode ser null." };
            }

            if (string.IsNullOrWhiteSpace(processedFilesOnError))
            {
                taskActions.Success = false;
                taskActions.Message = "Caminho da pasta de erro não pode ser nulo ou vazio.";
                return taskActions;
            }

            if (string.IsNullOrWhiteSpace(taskActions.Argument1))
            {
                taskActions.Success = false;
                taskActions.Message = "Caminho de origem (Argument1) não pode ser nulo ou vazio.";
                return taskActions;
            }

            try
            {
                var files = taskActions.Argument1.GetFiles();

                if (files == null || !files.Any())
                {
                    taskActions.Success = true;
                    taskActions.Message = "Nenhum arquivo encontrado para mover para pasta de erro.";
                    return taskActions;
                }

                // Verifica se o diretório de destino existe, se não existir cria
                if (!Directory.Exists(processedFilesOnError))
                {
                    Directory.CreateDirectory(processedFilesOnError);
                    taskActions.Message += $"Diretório de erro criado: {processedFilesOnError}\r\n";
                }

                int movedFiles = 0;
                int totalFiles = files.Count();

                foreach (var file in files)
                {
                    if (file == null || !file.Exists)
                    {
                        taskActions.Message += $"Arquivo não existe ou é inválido: {file?.FullName ?? "null"}\r\n";
                        continue;
                    }

                    try
                    {
                        var destinationPath = Path.Combine(processedFilesOnError, file.Name);

                        // Antes de mover, verifica se o arquivo já existe no destino
                        if (File.Exists(destinationPath))
                        {
                            // Cria um nome único para evitar sobrescrever
                            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                            string nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
                            string extension = Path.GetExtension(file.Name);
                            string uniqueName = $"{nameWithoutExt}_{timestamp}{extension}";
                            destinationPath = Path.Combine(processedFilesOnError, uniqueName);
                            
                            taskActions.Message += $"Arquivo já existia no destino, renomeado para: {uniqueName}\r\n";
                        }

                        // Move o arquivo para a pasta de destino
                        File.Move(file.FullName, destinationPath);
                        taskActions.Message += $"Arquivo {file.Name} movido com sucesso para pasta de erro.\r\n";
                        movedFiles++;
                    }
                    catch (IOException ex)
                    {
                        taskActions.Message += $"Erro de I/O ao mover arquivo {file.Name}: {ex.Message}\r\n";
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        taskActions.Message += $"Acesso negado ao mover arquivo {file.Name}: {ex.Message}\r\n";
                    }
                    catch (Exception ex)
                    {
                        taskActions.Message += $"Erro inesperado ao mover arquivo {file.Name}: {ex.Message}\r\n";
                    }
                }

                // Considera sucesso se moveu pelo menos um arquivo ou se não havia arquivos
                taskActions.Success = movedFiles > 0 || totalFiles == 0;
                
                if (movedFiles > 0)
                {
                    taskActions.Message += $"Total de {movedFiles} de {totalFiles} arquivo(s) movido(s) para pasta de erro.";
                }
                else if (totalFiles > 0)
                {
                    taskActions.Message += "Nenhum arquivo foi movido com sucesso para pasta de erro.";
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
                taskActions.Message = $"Erro inesperado ao mover arquivos para pasta de erro: {ex.Message}";
            }

            return taskActions;
        }
    }
}
