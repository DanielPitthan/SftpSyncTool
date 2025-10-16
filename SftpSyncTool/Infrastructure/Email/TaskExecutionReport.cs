namespace Infrastructure.Email
{
    /// <summary>
    /// Representa o relatório de execução de tarefas para notificação por e-mail
    /// </summary>
    public class TaskExecutionReport
    {
        public string FolderName { get; set; } = string.Empty;
        public DateTime ExecutionDate { get; set; } = DateTime.Now;
        public List<string> FilesProcessed { get; set; } = new();
        public List<string> FilesWithError { get; set; } = new();
        public List<TaskExecutionDetail> ExecutionDetails { get; set; } = new();
        public bool HasProcessedFiles => FilesProcessed.Any();
    }

    /// <summary>
    /// Detalhe de execução de uma tarefa específica
    /// </summary>
    public class TaskExecutionDetail
    {
        public string TaskType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
