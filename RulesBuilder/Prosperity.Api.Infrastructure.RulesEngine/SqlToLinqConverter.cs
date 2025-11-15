using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace Prosperity.Api.Infrastructure.RulesEngine;

public class SqlToLinqConverter : ISqlToLinqConverter
{
    public Expression<Func<T, bool>> ConvertToExpression<T>(string sqlWhereClause)
    {
        if (string.IsNullOrWhiteSpace(sqlWhereClause))
        {
            return _ => true;
        }
        var dynamicExpression = ConvertToDynamicExpression(sqlWhereClause, typeof(T));
        System.Console.WriteLine($"Converted SQL expression: {sqlWhereClause} -> {dynamicExpression}");
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
            System.Console.WriteLine($"Error parsing expression: {ex.Message}");
            System.Console.WriteLine($"Expression: {dynamicExpression}");
            throw;
        }
    }

    private string ConvertToDynamicExpression(string sql, Type entityType)
    {
        var result = sql.Trim();
        if (result.StartsWith('(') && result.EndsWith(')'))
        {
            var depth = 0;
            var shouldRemove = true;
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] == '(') depth++;
                else if (result[i] == ')') depth--;
                if (depth == 0 && i < result.Length - 1)
                {
                    shouldRemove = false;
                    break;
                }
            }
            if (shouldRemove && depth == 0)
            {
                result = result.Substring(1, result.Length - 2).Trim();
            }
        }
        var properties = entityType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        result = ConvertNestedPropertyPaths(result, entityType);
        foreach (var prop in properties)
        {
            var camelCase = ToCamelCase(prop.Name);
            var pascalCase = prop.Name;
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(camelCase)}\b(?!\s*\.)",
                pascalCase,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        result = ConvertLikeOperator(result);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\bgreaterthan\b", ">", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\bgreaterthanorequal\b", ">=", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\blessthan\b", "<", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\blessthanorequal\b", "<=", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\bequal\b", "==", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\bnotequal\b", "<>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\b(and|or|not)\b",
            m => m.Value.ToUpperInvariant(),
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = result.Replace("!=", "<>", StringComparison.Ordinal);
        result = ConvertStringValues(result, entityType);
        return result;
    }

    private string ConvertNestedPropertyPaths(string sql, Type entityType)
    {
        var result = sql;
        var nestedPathPattern = @"\b(\w+)\s*\.\s*(\w+)\b";
        result = System.Text.RegularExpressions.Regex.Replace(result, nestedPathPattern, match =>
        {
            var firstSegment = match.Groups[1].Value;
            var secondSegment = match.Groups[2].Value;
            var firstProp = entityType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .FirstOrDefault(p => string.Equals(p.Name, firstSegment, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(ToCamelCase(p.Name), firstSegment, StringComparison.OrdinalIgnoreCase));
            if (firstProp != null)
            {
                var firstPropName = firstProp.Name;
                var propType = Nullable.GetUnderlyingType(firstProp.PropertyType) ?? firstProp.PropertyType;
                var nestedProperties = propType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var secondProp = nestedProperties.FirstOrDefault(p =>
                    string.Equals(p.Name, secondSegment, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ToCamelCase(p.Name), secondSegment, StringComparison.OrdinalIgnoreCase));
                if (secondProp != null)
                {
                    var secondPropName = secondProp.Name;
                    return $"{firstPropName}.{secondPropName}";
                }
            }
            return match.Value;
        }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return result;
    }

    private string ConvertLikeOperator(string sql)
    {
        var likePattern = @"(\w+(?:\.\w+)?)\s+like\s+'([^']*)'";
        return System.Text.RegularExpressions.Regex.Replace(sql, likePattern, match =>
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
        }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private string ConvertStringValues(string sql, Type entityType)
    {
        var result = sql;
        var comparisonPattern = @"(\w+(?:\.\w+)?)\s*(==|!=|<>|>=|<=|>|<)\s*'([^']*)'";
        result = System.Text.RegularExpressions.Regex.Replace(result, comparisonPattern, match =>
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
        result = System.Text.RegularExpressions.Regex.Replace(result, @"'([^']*)'",
            m => $"\"{m.Groups[1].Value}\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return result;
    }

    private Type? GetPropertyType(string propertyPath, Type entityType)
    {
        var parts = propertyPath.Split('.');
        var currentType = entityType;
        foreach (var part in parts)
        {
            var prop = currentType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .FirstOrDefault(p => string.Equals(p.Name, part, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(ToCamelCase(p.Name), part, StringComparison.OrdinalIgnoreCase));
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
}
