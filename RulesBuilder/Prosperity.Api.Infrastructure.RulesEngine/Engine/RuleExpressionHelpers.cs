using System.Collections;
using System.Collections.Generic;
using System.Linq.Dynamic.Core.CustomTypeProviders;

namespace Prosperity.Api.Infrastructure.RulesEngine.Engine;

[DynamicLinqType]
public static class RuleExpressionHelpers
{
    public static bool ContainsAny(IEnumerable? source, IEnumerable? candidates)
    {
        if (source is null || candidates is null)
        {
            return false;
        }

        var lookup = new HashSet<object?>();
        foreach (var candidate in candidates)
        {
            lookup.Add(candidate);
        }

        foreach (var item in source)
        {
            if (lookup.Contains(item))
            {
                return true;
            }
        }

        return false;
    }
}
