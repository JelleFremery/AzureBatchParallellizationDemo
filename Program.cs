using AzureBatchParallellizationDemo;
using AzureBatchParallellizationDemo.Extensions;
using Microsoft.Extensions.DependencyInjection;

using var serviceProvider = new ServiceCollection()
    .ConfigureServices().BuildServiceProvider();
await serviceProvider.GetService<App>().RunAsync(args);