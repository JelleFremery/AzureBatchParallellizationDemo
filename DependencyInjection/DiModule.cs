using AzureBatchParallellizationDemo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AzureBatchParallellizationDemo.DependencyInjection;

public static class DiModule
{
    public static IServiceCollection Configure(IServiceCollection services)
    {
        services.AddTransient<App>();
        services.AddTransient<StorageService>();
        services.AddTransient<BatchService>();

        return services;
    }
}
