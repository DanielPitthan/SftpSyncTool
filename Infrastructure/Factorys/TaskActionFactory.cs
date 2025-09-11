using Infrastructure.Extensions;
using Models.Enums;
using Models.MappingTasks;

namespace Infrastructure.Factorys
{
    public static class TaskActionFactory
    {
        public static TaskActions? CreateTaskAction(string task, FolderMap folderMap, string taskName)
        {
            if (string.IsNullOrEmpty(task))
            {
                return null;
            }

            //Localiza o tipo de tarefa a partir da string e as variáveis de argumento, 
            //podemos ter até até 5 varáveis de argumento
            var listOfArguments = task.Split(":");

            //A primeira variável é o tipo de tarefa sempre 
            //Valida se é um tipo de tarefa válido 
            if (listOfArguments.Length == 0 || string.IsNullOrEmpty(listOfArguments[0]))
            {
                return null;
            }
            //verifica se está em TypeOfTasks
            if (!Enum.TryParse(listOfArguments[0], out TypeOfTasks typeOfTasks))
            {
                return null;
            }


            TaskActions action = new TaskActions(typeOfTasks, taskName);


            //A partir daqui, sabemos que temos um tipo de tarefa válido
            //Agora, precisamos mapear os argumentos
            for (int i = 1; i < listOfArguments.Length && i <= 5; i++)
            {
                var argument = listOfArguments[i].Trim()
                                                        .ExtractVariable()
                                                        .GetValue(folderMap); 
              
                switch (i)
                {
                    case 1:
                        action.Argument1 = argument;
                        break;
                    case 2:
                        action.Argument2 = argument;
                        break;
                    case 3:
                        action.Argument3 = argument;
                        break;
                    case 4:
                        action.Argument4 = argument;
                        break;
                    case 5:
                        action.Argument5 = argument;
                        break;
                }
            }

            return action;
        }
    }
}
