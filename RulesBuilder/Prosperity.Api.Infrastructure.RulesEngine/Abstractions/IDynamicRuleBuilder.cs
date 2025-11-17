using System;
using NRules.RuleModel;

namespace Prosperity.Api.Infrastructure.RulesEngine.Abstractions;

public interface IDynamicRuleBuilder
{
    IRuleDefinition BuildRule<TFact>(string condition, object output, string? ruleName = null);
    IRuleDefinition BuildRule(Type factType, string condition, object output, string? ruleName = null);
}
