namespace Infrastructure.Extensions
{
    public static class GetFilesFromPath
    {
        public static IEnumerable<FileInfo>? GetFiles(this string folderPath)
        {
            // Verifica se a string é um path do sistema válido
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("O caminho não pode ser nulo ou vazio.", nameof(folderPath));
            }

            if (!Path.IsPathRooted(folderPath))
            {
                throw new ArgumentException("O caminho deve ser um caminho absoluto válido.", nameof(folderPath));
            }

            try
            {
                Path.GetFullPath(folderPath);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException("O caminho contém caracteres inválidos.", nameof(folderPath));
            }
            catch (NotSupportedException)
            {
                throw new ArgumentException("O caminho não é válido.", nameof(folderPath));
            }

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"O diretório '{folderPath}' não foi encontrado.");
            }

            DirectoryInfo dir = new DirectoryInfo(folderPath);
            IEnumerable<FileInfo>? files = dir.EnumerateFiles();
            return files;
        }
    }
}
