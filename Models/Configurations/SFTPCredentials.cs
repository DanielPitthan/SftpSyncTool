namespace Models.Configurations
{
    public static class SFTPCredentials
    {
        // Remove the static constructor with parameters  
        // Initialize properties with default values to avoid CS8618  
        public static string SFTPUrl { get; set; } = string.Empty;
        public static string UsuarioSFTP { get; set; } = string.Empty;
        public static string Senha { get; set; } = string.Empty;
        public static int Port { get; set; } = 22;
    }
}
