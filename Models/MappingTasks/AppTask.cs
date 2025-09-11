namespace Models.MappingTasks
{
    public class AppTask
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public IEnumerable<FolderMap> FolderMaps { get; set; }
    }
}
