using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Services.CustomLogger
{
    public static class FileLoggerExtensions
    {
        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, Action<FileLoggerConfiguration>? configure = null)
        {
            var config = new FileLoggerConfiguration();
            configure?.Invoke(config);

            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();

            return builder;
        }

        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, FileLoggerConfiguration config)
        {
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();

            return builder;
        }

        public static ILoggingBuilder AddCustomConsole(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<ILoggerProvider, ConsoleLoggerProvider>();
            return builder;
        }
    }
}