using Models.MappingTasks;

namespace Infrastructure.Factorys
{
    public static class InspectFileFactory
    {
        /// <summary>
        /// Faz a inspeção do arquivo conforme a instrução passada, Linha, inicio e fim 
        /// </summary>
        /// <param name="fileFullName"></param>
        /// <param name="inspectInstruction"></param>
        /// <returns>Retorna os dados inspecionados</returns>
        public static string Inspect(string fileFullName, string inspectInstruction)
        {
            if (string.IsNullOrWhiteSpace(fileFullName))
            {
                return "";
            }
            if (!File.Exists(fileFullName))
            {
                return "";
            }

            var textLines = File.ReadAllLines(fileFullName);
            if (textLines.Length == 0)
            {
                return "";
            }

            int inicio =0;
            int fim = 0;
            int linhaAInspecionar = 0;
            var getLine = int.TryParse(inspectInstruction.Substring(1, 5), out linhaAInspecionar);         
            var getInicio = int.TryParse(inspectInstruction.Substring(7, 4), out inicio);
            var getFim = int.TryParse(inspectInstruction.Substring(12, 4), out fim);

            if (!getInicio || !getFim || !getLine)
            {
                return "";
            }
            inicio--;


            string? content = textLines[linhaAInspecionar-1].Substring(inicio, fim- inicio) ?? string.Empty;

            return content;
        }
    }
}
