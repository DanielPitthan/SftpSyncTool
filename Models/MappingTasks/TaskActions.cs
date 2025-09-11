using Models.Enums;

namespace Models.MappingTasks
{
    public class TaskActions
    {
        public bool ShouldInspect = false;
        public string Inspect_VAR = "";
        public string InspectPartOfFile;

        public TaskActions() { }
        public TaskActions(TypeOfTasks typeOfTasks, string name)
        {
            Action = typeOfTasks;
            Name = name;
            Argument1 = string.Empty;
            Argument2 = string.Empty;
            Argument3 = string.Empty;
            Argument4 = string.Empty;
            Argument5 = string.Empty;
        }

        public TaskActions(TypeOfTasks typeOfTasks, string name, string argument1)
        {
            Action = typeOfTasks;
            Name = name;
            Argument1 = argument1;
            Argument2 = string.Empty;
            Argument3 = string.Empty;
            Argument4 = string.Empty;
            Argument5 = string.Empty;
        }
        public TaskActions(TypeOfTasks typeOfTasks, string name, string argument1, string argument2)
        {
            Action = typeOfTasks;
            Name = name;
            Argument1 = argument1;
            Argument2 = argument2;
            Argument3 = string.Empty;
            Argument4 = string.Empty;
            Argument5 = string.Empty;
        }
        public TaskActions(TypeOfTasks typeOfTasks, string name, string argument1, string argument2, string argument3)
        {
            Action = typeOfTasks;
            Name = name;
            Argument1 = argument1;
            Argument2 = argument2;
            Argument3 = argument3;
            Argument4 = string.Empty;
            Argument5 = string.Empty;
        }
        public TaskActions(TypeOfTasks typeOfTasks, string name, string argument1, string argument2, string argument3, string argument4)
        {
            Action = typeOfTasks;
            Name = name;
            Argument1 = argument1;
            Argument2 = argument2;
            Argument3 = argument3;
            Argument4 = argument4;
            Argument5 = string.Empty;
        }
        public TaskActions(TypeOfTasks typeOfTasks, string name, string argument1, string argument2, string argument3, string argument4, string argument5)
        {
            Action = typeOfTasks;
            Name = name;
            Argument1 = argument1;
            Argument2 = argument2;
            Argument3 = argument3;
            Argument4 = argument4;
            Argument5 = argument5;
        }

        public TypeOfTasks Action { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Success { get; set; } = false;
        public string Message { get; set; } = string.Empty;
        public string Argument1 { get; set; }
        public string Argument2 { get; set; }
        public string Argument3 { get; set; }
        public string Argument4 { get; set; }
        public string Argument5 { get; set; }
        public IList<string> FilesProcessed { get; set; } = new List<string>();
    }
}
