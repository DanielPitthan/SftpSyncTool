namespace Infrastructure.Factorys
{
    public static class IsLocalPath
    {

        /// <summary>
        /// Verifica se o caminho de destino é um diretório local
        /// </summary>
        /// <param name="destinationPath">Caminho de destino</param>
        /// <returns>True se for um caminho local, False se for SFTP</returns>
        public static bool Execute(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
                return false;           

            // Verifica se é um caminho Windows (ex: C:\, D:\, \\servidor\compartilhamento)
            if (Path.IsPathRooted(destinationPath))
                return true;

            // Verifica se é um caminho Unix/Linux absoluto (ex: /home/user)
            if (destinationPath.StartsWith("/") && !destinationPath.Contains("@"))
                return true;

            // Verifica se é um caminho relativo local (ex: ./pasta, ../pasta)
            if (destinationPath.StartsWith("./") || destinationPath.StartsWith("../"))
                return true;

            // Se contém caracteres típicos de SFTP ou não é um caminho absoluto, assume SFTP
            return false;
        }
    }
}