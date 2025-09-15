using Infrastructure;
using Models.Configurations;
using Services.CustomLogger;

namespace CopyToSFTPObserver
{
    public class Program
    {
        public static void Main(string[] args)
        {
            HostApplicationBuilder? builder = Host.CreateApplicationBuilder(args);

            // Configurar a classe de configuração
            builder.Services.Configure<AppSettings>(builder.Configuration);

            AppSettings appSettings = new();
            builder.Configuration.Bind(appSettings);

            //Armazenar as credenciais SFTP na classe statica SFTPCredentials
            SFTPCredentials.SFTPUrl = appSettings.SFTPUrl;
            SFTPCredentials.UsuarioSFTP = appSettings.UsuarioSFTP;
            SFTPCredentials.Senha = appSettings.Senha;
            SFTPCredentials.Port = appSettings.Port;

            EmailCredentials.EMAIL_FROM = appSettings.EMAIL_FROM;
            EmailCredentials.EMAIL_HOST = appSettings.EMAIL_HOST;
            EmailCredentials.EMAIL_PORT = appSettings.EMAIL_PORT;
            EmailCredentials.EMAIL_SENHA = appSettings.EMAIL_SENHA;


            // Configurar o logger personalizado
            builder.Logging.ClearProviders(); // Remove providers padrão
            builder.Logging.AddFileLogger((config) =>
            {
                config.LogDirectory = appSettings.LogFile;
                config.LogFilePrefix = "processlog";
                config.MaxFileSizeInBytes = 5 * 1024 * 1024; // 5MB
                config.MaxRetainedFiles = 1;
            });

            // Adicionar console logger personalizado com mesmo formato do arquivo de log
            builder.Logging.AddCustomConsole();
            builder.Services.AddHostedService<Worker>();

            builder.Services.AddSingleton<AppTaskMapperConfigurator>();
            builder.Services.AddHostedService<HttpServerWorker>(); // <- servidor HTTP

            var host = builder.Build();
            host.Run();
        }
    }
}