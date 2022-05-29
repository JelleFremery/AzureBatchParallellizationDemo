using AzureBatchParallellizationDemo.DependencyInjection;
using AzureBatchParallellizationDemo.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;

namespace AzureBatchParallellizationDemo.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        // configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
        });

        // build config
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        services.Configure<BatchSettings>(configuration.GetSection("Batch"));
        services.Configure<BatchJobSettings>(configuration.GetSection("BatchJobs"));
        services.Configure<StorageSettings>(configuration.GetSection("Storage"));

        DiModule.Configure(services);

        return services;
    }
}