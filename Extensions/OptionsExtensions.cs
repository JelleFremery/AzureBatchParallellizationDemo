using Microsoft.Extensions.Options;

namespace AzureBatchParallellizationDemo.Extensions;

public static class OptionsExtensions
{
    public static T GuardedValue<T>(this IOptions<T> options) where T : class
    {
        return options.Guard().Value;
    }
}
