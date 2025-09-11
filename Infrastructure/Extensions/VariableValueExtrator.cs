using Models.MappingTasks;

namespace Infrastructure.Extensions
{
    public static class VariableValueExtrator
    {
        public static string? GetValue(this string? variableName, FolderMap folderMap)
        {
            if (folderMap == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(variableName))
            {
                return null;
            }
           
            var typeOfFolderMap = folderMap.GetType();
            var properties = typeOfFolderMap.GetProperties();
            foreach (var property in properties)
            {
                if (property.Name.Equals(variableName))
                {
                    return property.GetValue(folderMap).ToString();
                }
            }

            return null;
        }
    }
}
