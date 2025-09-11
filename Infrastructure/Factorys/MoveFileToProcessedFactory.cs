using Infrastructure.Extensions;
using Models.MappingTasks;

namespace Infrastructure.Factorys
{
    public static class MoveFileToProcessedFactory
    {
        /// <summary>
        /// Move arquivos para a pasta de sucesso (processados)
        /// </summary>
        /// <param name="taskActions">Ação da tarefa</param>
        /// <returns>TaskActions com resultado da operação</returns>
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
                    taskActions.Message = "Nenhum arquivo encontrado para mover para pasta de sucesso.";
                    return taskActions;
                }

                // Verifica se o diretório de destino existe, se não existir cria
                if (!Directory.Exists(taskActions.Argument2))
                {
                    Directory.CreateDirectory(taskActions.Argument2);
                    taskActions.Message += $"Diretório de sucesso criado: {taskActions.Argument2}\r\n";
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
                        var destinationPath = Path.Combine(taskActions.Argument2, file.Name);

                        // Antes de mover, verifica se o arquivo já existe no destino
                        if (File.Exists(destinationPath))
                        {
                            try
                            {
                                // Verifica se os arquivos são iguais (mesmo tamanho)
                                var existingFileInfo = new FileInfo(destinationPath);
                                if (file.Length == existingFileInfo.Length)
                                {
                                    // Arquivos parecem ser iguais, remove o original e mantém o existente
                                    File.Delete(file.FullName);
                                    taskActions.Message += $"Arquivo {file.Name} já existe no destino com mesmo tamanho, arquivo original removido.\r\n";
                                    movedFiles++;
                                    continue;
                                }
                                else
                                {
                                    // Cria backup do arquivo existente antes de sobrescrever
                                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                                    string nameWithoutExt = Path.GetFileNameWithoutExtension(destinationPath);
                                    string extension = Path.GetExtension(destinationPath);
                                    string backupPath = Path.Combine(taskActions.Argument2, $"{nameWithoutExt}_backup_{timestamp}{extension}");
                                    File.Move(destinationPath, backupPath);
                                    taskActions.Message += $"Arquivo existente renomeado para backup: {Path.GetFileName(backupPath)}\r\n";
                                }
                            }
                            catch (Exception ex)
                            {
                                taskActions.Message += $"Erro ao verificar arquivo existente {file.Name}: {ex.Message}\r\n";
                                // Continua tentando mover mesmo com erro na verificação
                            }
                        }

                        // Move o arquivo para a pasta de destino
                        File.Move(file.FullName, destinationPath);
                        taskActions.Message += $"Arquivo {file.Name} movido com sucesso para pasta de sucesso.\r\n";
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
                    taskActions.Message += $"Total de {movedFiles} de {totalFiles} arquivo(s) movido(s) para pasta de sucesso.";
                }
                else if (totalFiles > 0)
                {
                    taskActions.Message += "Nenhum arquivo foi movido com sucesso para pasta de sucesso.";
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
                taskActions.Message = $"Erro inesperado ao mover arquivos para pasta de sucesso: {ex.Message}";
            }

            return taskActions;
        }
    }
}
