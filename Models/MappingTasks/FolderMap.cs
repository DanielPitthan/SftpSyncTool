namespace Models.MappingTasks
{
    public class FolderMap
    {

     
        public string Name { get; set; }
        public string FolderPathOrigin { get; set; }
        public string SFTPPathDestination { get; set; }
        public string ProcessedFilesOnError { get; set; }
        public string ProcessedFilesOnSuccess { get; set; }
        public string EmailNotify { get; set; }
        public string InspectLocation { get; set; }

        public IEnumerable<TasksMap> TasksMaps { get; set; }

        /// <summary>
        /// Retorna os arquivos da pasta mapeada
        /// </summary>
        /// <returns></returns>
        public IEnumerable<FileInfo>? GetFiles()
        {
            DirectoryInfo dir = new DirectoryInfo(FolderPathOrigin);
            IEnumerable<FileInfo>? files = dir.EnumerateFiles();
            return files;
        }


    }
}
