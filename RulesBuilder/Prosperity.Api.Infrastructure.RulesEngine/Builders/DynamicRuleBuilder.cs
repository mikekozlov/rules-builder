using System.Linq.Expressions;
using NRules.RuleModel;
using NRules.RuleModel.Builders;
using Prosperity.Api.Infrastructure.RulesEngine.Abstractions;

namespace Prosperity.Api.Infrastructure.RulesEngine.Builders;

/// <summary>
/// Builds NRules rule definitions dynamically from SQL conditions and output objects
/// </summary>
public class DynamicRuleBuilder : IDynamicRuleBuilder
{
    private readonly ISqlToLinqConverter _sqlConverter;

    public DynamicRuleBuilder(ISqlToLinqConverter sqlConverter)
    {
        _sqlConverter = sqlConverter;
    }

    /// <summary>
    /// Creates an NRules rule definition from a SQL condition and output object
    /// </summary>
    /// <typeparam name="TFact">The fact type to match against (e.g., Patient)</typeparam>
    /// <param name="condition">SQL WHERE clause condition from React Query Builder</param>
    /// <param name="output">The output object to insert when condition matches</param>
    /// <param name="ruleName">Optional rule name</param>
    /// <returns>An IRuleDefinition that can be serialized and stored</returns>
    public IRuleDefinition BuildRule<TFact>(string condition, object output, string? ruleName = null)
    {
        var predicate = _sqlConverter.ConvertToExpression<TFact>(condition);
        var builder = new RuleBuilder();
        if (!string.IsNullOrWhiteSpace(ruleName))
        {
            builder.Name(ruleName);
        }
        else
        {
            builder.Name($"Dynamic Rule: {condition}");
        }
        var lhs = builder.LeftHandSide();
        var parameterName = predicate.Parameters[0].Name;
        var factPattern = lhs.Pattern(typeof(TFact), parameterName);
        factPattern.Condition(predicate);
        var rhs = builder.RightHandSide();
        var insertAction = CreateInsertAction<TFact>(output);
        rhs.Action(insertAction);
        return builder.Build();
    }

    /// <summary>
    /// Creates an NRules rule definition from a SQL condition and output object (with runtime type)
    /// </summary>
    public IRuleDefinition BuildRule(Type factType, string condition, object output, string? ruleName = null)
    {
        var method = typeof(DynamicRuleBuilder).GetMethod(nameof(BuildRule), new[] { typeof(string), typeof(object), typeof(string) })
                     ?? throw new InvalidOperationException("Could not find BuildRule method");
        var genericMethod = method.MakeGenericMethod(factType);
        return (IRuleDefinition)genericMethod.Invoke(this, new[] { condition, output, ruleName })!;
    }

    private Expression<Action<IContext>> CreateInsertAction<TFact>(object output)
    {
        var contextParam = Expression.Parameter(typeof(IContext), "ctx");
        var insertMethod = typeof(IContext).GetMethod("Insert", new[] { typeof(object) })
                           ?? throw new InvalidOperationException("Could not find Insert method on IContext");
        var outputConstant = Expression.Constant(output);
        var insertCall = Expression.Call(contextParam, insertMethod, outputConstant);
        return Expression.Lambda<Action<IContext>>(insertCall, contextParam);
    }
}
