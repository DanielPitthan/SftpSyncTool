namespace Services.CustomLogger
{
    public class FileLoggerConfiguration
    {
        public string LogDirectory { get; set; } = "copyToSFTPObserverLogger";
        public string LogFilePrefix { get; set; } = "processlog";
        public long MaxFileSizeInBytes { get; set; } = 5 * 1024 * 1024; // 5MB
        public int MaxRetainedFiles { get; set; } = 1;
    }
}