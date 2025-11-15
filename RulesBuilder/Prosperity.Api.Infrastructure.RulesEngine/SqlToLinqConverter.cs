using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Prosperity.Api.Infrastructure.RulesEngine;

public class SqlToLinqConverter : ISqlToLinqConverter
{
    private static readonly Regex LikeRegex = new(@"(?<prop>\w+(?:\.\w+)*)\s+like\s+'(?<pattern>[^']*)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ComparisonRegex = new(@"(?<prop>\w+(?:\.\w+)*)\s*(?<op>=|==|!=|<>|>=|<=|>|<)\s*'(?<value>[^']*)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NullComparisonRegex = new(@"(?<prop>\w+(?:\.\w+)*)\s+is\s+(?<neg>not\s+)?null", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InOperatorRegex = new(@"(?<prop>\w+(?:\.\w+)*)\s+(?<neg>not\s+)?in\s*\((?<values>[^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OperatorTokenRegex = new(@"\b(greaterthanorequal|greaterthan|lessthanorequal|lessthan|equal|notequal)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BooleanOperatorRegex = new(@"\b(and|or|not)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly ConcurrentDictionary<Type, EntityMetadata> _metadataCache = new();

    public Expression<Func<T, bool>> ConvertToExpression<T>(string sqlWhereClause)
    {
        if (string.IsNullOrWhiteSpace(sqlWhereClause))
        {
            return _ => true;
        }
        var dynamicExpression = ConvertToDynamicExpression(sqlWhereClause, typeof(T));
        var parameter = Expression.Parameter(typeof(T), "p");
        try
        {
            var config = new ParsingConfig
            {
                IsCaseSensitive = false,
                UseParameterizedNamesInDynamicQuery = false
            };
            var lambda = DynamicExpressionParser.ParseLambda(config, new[] { parameter }, typeof(bool), dynamicExpression);
            return (Expression<Func<T, bool>>)lambda;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse expression '{dynamicExpression}': {ex.Message}", ex);
        }
    }

    private string ConvertToDynamicExpression(string sql, Type entityType)
    {
        var trimmed = RemoveOuterParentheses(sql);
        var operatorNormalized = NormalizeOperators(trimmed);
        var nullHandled = ConvertNullComparisons(operatorNormalized, entityType);
        var likeHandled = ConvertLikeOperator(nullHandled, entityType);
        var inHandled = ConvertInOperator(likeHandled, entityType);
        return ConvertComparisonValues(inHandled, entityType);
    }

    private string ConvertLikeOperator(string sql, Type entityType)
    {
        return LikeRegex.Replace(sql, match =>
        {
            var property = AlignPropertyPath(match.Groups["prop"].Value, entityType, out var propertyType);
            if (propertyType != typeof(string))
            {
                throw new InvalidOperationException($"LIKE can only be used with string properties. Property '{property}' is of type '{propertyType.Name}'.");
            }
            var pattern = match.Groups["pattern"].Value;
            if (ContainsInnerWildcards(pattern))
            {
                throw new InvalidOperationException($"LIKE pattern '{pattern}' is not supported. Only leading and trailing '%' wildcards are allowed.");
            }
            if (pattern.StartsWith('%') && pattern.EndsWith('%'))
            {
                var value = pattern.Trim('%');
                return $"{property}.Contains(\"{EscapeStringLiteral(value)}\")";
            }
            if (pattern.EndsWith('%'))
            {
                var value = pattern.TrimEnd('%');
                return $"{property}.StartsWith(\"{EscapeStringLiteral(value)}\")";
            }
            if (pattern.StartsWith('%'))
            {
                var value = pattern.TrimStart('%');
                return $"{property}.EndsWith(\"{EscapeStringLiteral(value)}\")";
            }
            return $"{property} == \"{EscapeStringLiteral(pattern)}\"";
        });
    }

    private string ConvertComparisonValues(string sql, Type entityType)
    {
        var result = ComparisonRegex.Replace(sql, match =>
        {
            var property = AlignPropertyPath(match.Groups["prop"].Value, entityType, out var propType);
            var op = NormalizeComparisonOperator(match.Groups["op"].Value);
            var value = match.Groups["value"].Value;
            var formattedValue = FormatLiteral(propType, value);
            return $"{property} {op} {formattedValue}";
        });
        return result;
    }

    private string ConvertInOperator(string sql, Type entityType)
    {
        return InOperatorRegex.Replace(sql, match =>
        {
            var property = AlignPropertyPath(match.Groups["prop"].Value, entityType, out var propertyType);
            var negated = match.Groups["neg"].Success;
            var rawValues = match.Groups["values"].Value;
            var parsedValues = ParseInValues(rawValues).ToArray();
            if (parsedValues.Length == 0)
            {
                throw new InvalidOperationException("IN operator requires at least one value.");
            }
            var formattedValues = parsedValues.Select(value => FormatLiteral(propertyType, value));
            var arrayExpression = $"new [] {{ {string.Join(", ", formattedValues)} }}";
            var containsExpression = $"{arrayExpression}.Contains({property})";
            return negated ? $"!{containsExpression}" : containsExpression;
        });
    }

    private string ConvertNullComparisons(string sql, Type entityType)
    {
        return NullComparisonRegex.Replace(sql, match =>
        {
            var property = AlignPropertyPath(match.Groups["prop"].Value, entityType, out var propType);
            var underlying = Nullable.GetUnderlyingType(propType);
            if (propType.IsValueType && underlying == null)
            {
                throw new InvalidOperationException($"Property '{property}' is not nullable and cannot be compared to NULL.");
            }
            var comparison = match.Groups["neg"].Success ? "!= null" : "== null";
            return $"{property} {comparison}";
        });
    }

    private bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
               type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) ||
               type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
               type == typeof(byte) || type == typeof(sbyte);
    }

    private string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;
        if (pascalCase.Length == 1)
            return pascalCase.ToLowerInvariant();
        return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
    }

    private string RemoveOuterParentheses(string sql)
    {
        var result = sql.Trim();
        while (result.Length > 1 && result.StartsWith('(') && result.EndsWith(')'))
        {
            var depth = 0;
            var shouldRemove = true;
            for (var i = 0; i < result.Length; i++)
            {
                if (result[i] == '(') depth++;
                else if (result[i] == ')') depth--;
                if (depth == 0 && i < result.Length - 1)
                {
                    shouldRemove = false;
                    break;
                }
            }
            if (!shouldRemove || depth != 0)
            {
                break;
            }
            result = result.Substring(1, result.Length - 2).Trim();
        }
        return result;
    }

    private string NormalizeOperators(string sql)
    {
        var converted = OperatorTokenRegex.Replace(sql, match => match.Value.ToLowerInvariant() switch
        {
            "greaterthan" => ">",
            "greaterthanorequal" => ">=",
            "lessthan" => "<",
            "lessthanorequal" => "<=",
            "equal" => "==",
            _ => "<>"
        });
        converted = BooleanOperatorRegex.Replace(converted, match => match.Value.ToUpperInvariant());
        return converted.Replace("!=", "<>", StringComparison.Ordinal);
    }

    private PropertyInfo? FindProperty(Type type, string name)
    {
        var metadata = GetMetadata(type);
        return metadata.PropertiesByAlias.TryGetValue(name, out var propertyInfo) ? propertyInfo : null;
    }

    private string AlignPropertyPath(string propertyPath, Type entityType, out Type propertyType)
    {
        var currentType = entityType;
        var canonicalSegments = new List<string>();
        foreach (var segment in propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var property = FindProperty(currentType, segment) ?? throw new InvalidOperationException($"Property '{segment}' was not found on '{currentType.Name}'.");
            canonicalSegments.Add(property.Name);
            currentType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }
        propertyType = currentType;
        return string.Join('.', canonicalSegments);
    }

    private string NormalizeComparisonOperator(string op)
    {
        return op switch
        {
            "=" => "==",
            "<>" => "!=",
            _ => op
        };
    }

    private string FormatLiteral(Type targetType, string rawValue)
    {
        if (targetType == typeof(string))
        {
            return $"\"{EscapeStringLiteral(rawValue)}\"";
        }
        if (targetType == typeof(bool))
        {
            if (bool.TryParse(rawValue, out var boolValue))
            {
                return boolValue ? "true" : "false";
            }
            throw new InvalidOperationException($"Value '{rawValue}' could not be converted to boolean.");
        }
        if (IsNumericType(targetType))
        {
            if (decimal.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
            {
                var converted = Convert.ChangeType(decimalValue, targetType, CultureInfo.InvariantCulture);
                return Convert.ToString(converted, CultureInfo.InvariantCulture)!;
            }
            throw new InvalidOperationException($"Value '{rawValue}' could not be converted to {targetType.Name}.");
        }
        if (targetType == typeof(DateTime))
        {
            if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out var dateTime))
            {
                return $"DateTime.Parse(\"{dateTime.ToString("O", CultureInfo.InvariantCulture)}\")";
            }
            throw new InvalidOperationException($"Value '{rawValue}' could not be converted to DateTime.");
        }
        if (targetType == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out var dateTimeOffset))
            {
                return $"DateTimeOffset.Parse(\"{dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)}\")";
            }
            throw new InvalidOperationException($"Value '{rawValue}' could not be converted to DateTimeOffset.");
        }
        if (targetType == typeof(Guid))
        {
            if (Guid.TryParse(rawValue, out var guid))
            {
                return $"Guid.Parse(\"{guid}\")";
            }
            throw new InvalidOperationException($"Value '{rawValue}' could not be converted to Guid.");
        }
        if (targetType.IsEnum)
        {
            if (Enum.TryParse(targetType, rawValue, true, out var enumValue))
            {
                var numericValue = Convert.ToInt64(enumValue, CultureInfo.InvariantCulture);
                return numericValue.ToString(CultureInfo.InvariantCulture);
            }
            throw new InvalidOperationException($"Value '{rawValue}' could not be converted to enum '{targetType.Name}'.");
        }
        return $"\"{EscapeStringLiteral(rawValue)}\"";
    }

    private static bool ContainsInnerWildcards(string pattern)
    {
        if (pattern.Length == 0)
        {
            return false;
        }
        var trimmed = pattern.Trim('%');
        return trimmed.Contains('%') || pattern.Contains('_');
    }

    private string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private IEnumerable<string> ParseInValues(string values)
    {
        var result = new List<string>();
        var current = new List<char>();
        var inQuotes = false;
        foreach (var ch in values)
        {
            if (ch == '\'')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (ch == ',' && !inQuotes)
            {
                var item = new string(current.ToArray()).Trim();
                if (item.Length > 0)
                {
                    result.Add(item);
                }
                current.Clear();
                continue;
            }
            current.Add(ch);
        }
        var last = new string(current.ToArray()).Trim();
        if (last.Length > 0)
        {
            result.Add(last);
        }
        return result.Select(value => value.Trim());
    }

    private EntityMetadata GetMetadata(Type type)
    {
        return _metadataCache.GetOrAdd(type, t =>
        {
            var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var aliasMap = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in properties)
            {
                aliasMap[property.Name] = property;
                aliasMap[ToCamelCase(property.Name)] = property;
            }
            return new EntityMetadata(properties, aliasMap);
        });
    }

    private sealed record EntityMetadata(IReadOnlyCollection<PropertyInfo> Properties, IReadOnlyDictionary<string, PropertyInfo> PropertiesByAlias);
}
