using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Prosperity.Api.Infrastructure.RulesEngine;

public class SqlToLinqConverter
{
    private static readonly Regex NestedPathRegex = new(@"\b(\w+)\s*\.\s*(\w+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LikeRegex = new(@"(\w+(?:\.\w+)?)\s+like\s+'([^']*)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ComparisonRegex = new(@"(\w+(?:\.\w+)?)\s*(==|!=|<>|>=|<=|>|<)\s*'([^']*)'", RegexOptions.Compiled);
    private static readonly Regex OperatorTokenRegex = new(@"\b(greaterthanorequal|greaterthan|lessthanorequal|lessthan|equal|notequal)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BooleanOperatorRegex = new(@"\b(and|or|not)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex QuotedValueRegex = new(@"'([^']*)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
        var nestedResolved = ConvertNestedPropertyPaths(trimmed, entityType);
        var propertyAligned = ReplacePropertyNames(nestedResolved, entityType);
        var operatorNormalized = NormalizeOperators(propertyAligned);
        var likeHandled = ConvertLikeOperator(operatorNormalized);
        return ConvertStringValues(likeHandled, entityType);
    }

    private string ConvertNestedPropertyPaths(string sql, Type entityType)
    {
        return NestedPathRegex.Replace(sql, match =>
        {
            var firstSegment = match.Groups[1].Value;
            var secondSegment = match.Groups[2].Value;
            var firstProp = FindProperty(entityType, firstSegment);
            if (firstProp == null)
            {
                return match.Value;
            }
            var secondProp = FindProperty(Nullable.GetUnderlyingType(firstProp.PropertyType) ?? firstProp.PropertyType, secondSegment);
            return secondProp != null ? $"{firstProp.Name}.{secondProp.Name}" : match.Value;
        });
    }

    private string ConvertLikeOperator(string sql)
    {
        return LikeRegex.Replace(sql, match =>
        {
            var property = match.Groups[1].Value;
            var pattern = match.Groups[2].Value;
            if (pattern.StartsWith('%') && pattern.EndsWith('%'))
            {
                var value = pattern.Trim('%');
                return $"{property}.Contains(\"{value}\")";
            }
            else if (pattern.EndsWith('%'))
            {
                var value = pattern.TrimEnd('%');
                return $"{property}.StartsWith(\"{value}\")";
            }
            else if (pattern.StartsWith('%'))
            {
                var value = pattern.TrimStart('%');
                return $"{property}.EndsWith(\"{value}\")";
            }
            else
            {
                return $"{property} == \"{pattern}\"";
            }
        });
    }

    private string ConvertStringValues(string sql, Type entityType)
    {
        var result = ComparisonRegex.Replace(sql, match =>
        {
            var property = match.Groups[1].Value;
            var op = match.Groups[2].Value;
            var value = match.Groups[3].Value;
            var propType = GetPropertyType(property, entityType);
            if (propType != null)
            {
                if (IsNumericType(propType))
                {
                    if (int.TryParse(value, out var intValue))
                    {
                        return $"{property} {op} {intValue}";
                    }
                    if (double.TryParse(value, out var doubleValue))
                    {
                        return $"{property} {op} {doubleValue}";
                    }
                }
                else if (propType == typeof(bool))
                {
                    if (bool.TryParse(value, out var boolValue))
                    {
                        return $"{property} {op} {boolValue.ToString().ToLowerInvariant()}";
                    }
                }
            }
            return $"{property} {op} \"{value}\"";
        });
        return QuotedValueRegex.Replace(result,
            m => $"\"{m.Groups[1].Value}\"");
    }

    private Type? GetPropertyType(string propertyPath, Type entityType)
    {
        var parts = propertyPath.Split('.');
        var currentType = entityType;
        foreach (var part in parts)
        {
            var prop = FindProperty(currentType, part);
            if (prop == null)
                return null;
            currentType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        }
        return currentType;
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

    private string ReplacePropertyNames(string sql, Type entityType)
    {
        var metadata = GetMetadata(entityType);
        var result = sql;
        foreach (var property in metadata.Properties)
        {
            var pattern = $@"\b{Regex.Escape(ToCamelCase(property.Name))}\b(?!\s*\.)";
            result = Regex.Replace(result, pattern, property.Name, RegexOptions.IgnoreCase);
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
