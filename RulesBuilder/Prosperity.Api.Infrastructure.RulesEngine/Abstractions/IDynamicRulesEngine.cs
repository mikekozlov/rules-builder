using Prosperity.Api.Infrastructure.RulesEngine.Engine;
using Prosperity.Api.Infrastructure.RulesEngine.Models;

namespace Prosperity.Api.Infrastructure.RulesEngine.Abstractions;

public interface IDynamicRulesEngine<TFact, TOutput>
{
    Task<StoredRule> CreateRuleAsync(
        string ruleSetKey,
        string condition,
        TOutput outputTemplate,
        string? ruleName = null,
        string? domain = null,
        string? description = null,
        string? ruleSerialization = null,
        RuleMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    Task<EvaluationResult<TFact, TOutput>> EvaluateAsync(
        string ruleSetKey,
        TFact fact,
        CancellationToken cancellationToken = default);
}
