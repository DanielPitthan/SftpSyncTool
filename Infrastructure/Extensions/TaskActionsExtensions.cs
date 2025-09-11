using Infrastructure.Factorys;
using Models.MappingTasks;

namespace Infrastructure.Extensions
{
    public static class TaskActionsExtensions
    {


        /// <summary>
        /// Executa a operação de cópia de arquivos
        /// </summary>
        /// <param name="taskActions">Ação da tarefa</param>
        /// <returns>TaskActions com resultado da operação</returns>
        public static TaskActions ExecuteCopy(this TaskActions taskActions)
        {
            if (taskActions == null)
            {
                return new TaskActions { Success = false, Message = "TaskActions não pode ser null." };
            }

            try
            {
                return CopyToDestination.Copy(taskActions);
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro inesperado durante a cópia: {ex.Message}";
                return taskActions;
            }
        }

        /// <summary>
        /// Executa a verificação de arquivos
        /// </summary>
        /// <param name="taskActions">Ação da tarefa</param>
        /// <returns>TaskActions com resultado da operação</returns>
        public static TaskActions Check(this TaskActions taskActions)
        {
            if (taskActions == null)
            {
                return new TaskActions { Success = false, Message = "TaskActions não pode ser null." };
            }

            try
            {
                return CheckCopyResult.Execute(taskActions);
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro inesperado durante a verificação: {ex.Message}";
                return taskActions;
            }
        }

        /// <summary>
        /// Executa a movimentação de arquivos para pasta de sucesso
        /// </summary>
        /// <param name="taskActions">Ação da tarefa</param>
        /// <returns>TaskActions com resultado da operação</returns>
        public static TaskActions Move(this TaskActions taskActions)
        {
            if (taskActions == null)
            {
                return new TaskActions { Success = false, Message = "TaskActions não pode ser null." };
            }

            try
            {
                return MoveFileToProcessedFactory.Execute(taskActions);
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro inesperado durante a movimentação: {ex.Message}";
                return taskActions;
            }
        }

        /// <summary>
        /// Move arquivos para pasta de erro
        /// </summary>
        /// <param name="taskActions">Ação da tarefa</param>
        /// <param name="errorFolderPath">Caminho da pasta de erro</param>
        /// <returns>TaskActions com resultado da operação</returns>
        public static TaskActions MoveToErrorFolder(this TaskActions taskActions, string errorFolderPath)
        {
            if (taskActions == null)
            {
                return new TaskActions { Success = false, Message = "TaskActions não pode ser null." };
            }

            if (string.IsNullOrWhiteSpace(errorFolderPath))
            {
                taskActions.Success = false;
                taskActions.Message = "Caminho da pasta de erro não pode ser nulo ou vazio.";
                return taskActions;
            }

            try
            {
                return MoveFileToErrorFolderFactory.Execute(taskActions, errorFolderPath);
            }
            catch (Exception ex)
            {
                taskActions.Success = false;
                taskActions.Message = $"Erro inesperado ao mover para pasta de erro: {ex.Message}";
                return taskActions;
            }
        }
    }
}