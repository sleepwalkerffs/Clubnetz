using System.Linq.Expressions;
using Fusonic.Extensions.Common.Reflection;

namespace Bookennis.Api.Infrastructure.Utils.Sorting;

public static class SortParametersExtensions
{
    public static void RenameSortKey(this SortParameters? parameters, string key, string replacement)
    {
        if (parameters == null)
            return;

        var kv = parameters.SingleOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (kv.Key != null && parameters.Remove(kv.Key, out var value))
            parameters[replacement] = value;
    }

    public static void RenameSortKey<T>(this SortParameters? parameters, Expression<Func<T, object>> key, Expression<Func<T, object>> replacement)
    {
        if (parameters == null)
            return;

        parameters.RenameSortKey(PropertyUtil.GetName(key), PropertyUtil.GetName(replacement));
    }

}