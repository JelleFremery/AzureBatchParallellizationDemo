using System;
using System.Runtime.CompilerServices;

namespace AzureBatchParallellizationDemo.Extensions;

public static class ClassExtensions
{
    public static T Guard<T>(this T item, [CallerArgumentExpression("item")] string expression = default, string message = null)
        where T : class
    {
        if (item == null)
            throw new ArgumentNullException(
                expression ?? "<unknown>",
                message ?? (expression != null ? "Requires " + expression + " != null" : "RequiresArgNotNull() failed."));

        return item;
    }
}
