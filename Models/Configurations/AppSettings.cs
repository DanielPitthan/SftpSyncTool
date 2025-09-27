namespace CopyToSFTPObserver
{
    public class AppSettings
    {
        private string httpUrl = "http://localhost:8000/";//valor default
        public string HTTP_URL
        {
            get
            {
                if (string.IsNullOrEmpty(httpUrl))
                    return "http://localhost:8000/";
                return httpUrl;
            }
            set
            {
                httpUrl = value;
            }
        }
        public string EMAIL_FROM { get; set; }
        private string emailSenha = "";
        public string EMAIL_SENHA
        {
            get
            {
                if (string.IsNullOrEmpty(emailSenha))
                    return string.Empty;

                try
                {
                    return System
                            .Text
                            .Encoding
                            .UTF8
                            .GetString(Convert.FromBase64String(emailSenha));
                }
                catch (FormatException)
                {
                    // Se não for base64 válido, retorna a string original
                    return emailSenha;
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
            set
            {
                emailSenha = value;
            }
        }
        public int EMAIL_PORT { get; set; }
        public string EMAIL_HOST { get; set; }

        public LoggingConfiguration Logging { get; set; } = new();
        public int IntervaloEntreExecucoes { get; set; } = 500;
        public string LogFile { get; set; } = string.Empty;

        private string? sFTPUrl;
        public string SFTPUrl
        {
            get
            {
                if (string.IsNullOrEmpty(sFTPUrl))
                    return string.Empty;

                try
                {
                    return System
                            .Text
                            .Encoding
                            .UTF8
                            .GetString(Convert.FromBase64String(sFTPUrl));
                }
                catch (FormatException)
                {
                    // Se não for base64 válido, retorna a string original
                    return sFTPUrl;
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
            set
            {
                sFTPUrl = value;
            }
        }

        private string? usuarioSFTP;
        public string UsuarioSFTP
        {
            get
            {
                if (string.IsNullOrEmpty(usuarioSFTP))
                    return string.Empty;

                try
                {
                    return System
                            .Text
                            .Encoding
                            .UTF8
                            .GetString(Convert.FromBase64String(usuarioSFTP));
                }
                catch (FormatException)
                {
                    // Se não for base64 válido, retorna a string original
                    return usuarioSFTP;
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
            set
            {
                usuarioSFTP = value;
            }
        }

        private string? senha;
        public string Senha
        {
            get
            {
                if (string.IsNullOrEmpty(senha))
                    return string.Empty;

                try
                {
                    return System
                            .Text
                            .Encoding
                            .UTF8
                            .GetString(Convert.FromBase64String(senha));
                }
                catch (FormatException)
                {
                    // Se não for base64 válido, retorna a string original
                    return senha;
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
            set
            {
                senha = value;
            }
        }

        public int Port { get; set; } = 22;

        /// <summary>
        /// Valida se as configurações SFTP estão corretas
        /// </summary>
        /// <returns>True se as configurações estão válidas</returns>
        public bool IsValidSFTPConfiguration()
        {
            return !string.IsNullOrWhiteSpace(SFTPUrl) &&
                   !string.IsNullOrWhiteSpace(UsuarioSFTP) &&
                   !string.IsNullOrWhiteSpace(Senha) &&
                   Port > 0 && Port <= 65535;
        }

        /// <summary>
        /// Obtém uma descrição dos problemas de configuração SFTP
        /// </summary>
        /// <returns>Lista de problemas encontrados</returns>
        public List<string> GetSFTPConfigurationIssues()
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(SFTPUrl))
                issues.Add("URL do SFTP não configurada ou inválida");

            if (string.IsNullOrWhiteSpace(UsuarioSFTP))
                issues.Add("Usuário SFTP não configurado ou inválido");

            if (string.IsNullOrWhiteSpace(Senha))
                issues.Add("Senha SFTP não configurada ou inválida");

            if (Port <= 0 || Port > 65535)
                issues.Add($"Porta SFTP inválida: {Port}. Deve estar entre 1 e 65535");

            return issues;
        }
    }

    public class LoggingConfiguration
    {
        public LogLevelConfiguration LogLevel { get; set; } = new();
    }

    public class LogLevelConfiguration
    {
        public string Default { get; set; } = "Information";
        public string MicrosoftHostingLifetime { get; set; } = "Information";
    }
}