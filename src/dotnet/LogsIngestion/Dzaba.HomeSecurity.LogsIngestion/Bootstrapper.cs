using Serilog;

namespace Dzaba.HomeSecurity.LogsIngestion;

internal static class Bootstrapper
{
    private static readonly string SerilogOutputTemplate = "{Timestamp:dd.MM.yyyy HH:mm:ss} [{SourceContext}] [{ThreadId}] [{Level:u3}] - {Message:lj}{NewLine}{Exception}";

    public static IServiceCollection AddLogsIngestionLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        var logsFile = Path.Combine(logsFolder, "LogsIngestion.log");

        var logger = new LoggerConfiguration()
            .Enrich.WithThreadId()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: SerilogOutputTemplate)
            .WriteTo.File(logsFile, outputTemplate: SerilogOutputTemplate, rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true, fileSizeLimitBytes: 15 * 1024 * 1024)
            .CreateLogger();

        services.AddLogging(l => l.AddSerilog(logger, true));

        return services;
    }
}
