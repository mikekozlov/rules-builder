namespace Prosperity.Api.Infrastructure.RulesEngine;

public interface IDynamicRulesEngine<TFact, TOutput>
{
    Task<StoredRule> CreateRuleAsync(
        string ruleSetKey,
        string condition,
        TOutput outputTemplate,
        string? ruleName = null,
        CancellationToken cancellationToken = default);

    Task<EvaluationResult<TFact, TOutput>> EvaluateAsync(
        string ruleSetKey,
        TFact fact,
        CancellationToken cancellationToken = default);
}
