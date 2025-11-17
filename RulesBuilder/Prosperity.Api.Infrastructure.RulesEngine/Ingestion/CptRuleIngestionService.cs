using System.Linq;
using Prosperity.Api.Infrastructure.RulesEngine.Abstractions;
using Prosperity.Api.Infrastructure.RulesEngine.Models;

namespace Prosperity.Api.Infrastructure.RulesEngine.Ingestion;

public sealed class CptRuleIngestionService
{
    private readonly IDynamicRulesEngine<Encounter, CptCodeOutput> _rulesEngine;
    private readonly IRuleStore _ruleStore;

    public CptRuleIngestionService(IDynamicRulesEngine<Encounter, CptCodeOutput> rulesEngine, IRuleStore ruleStore)
    {
        _rulesEngine = rulesEngine;
        _ruleStore = ruleStore;
    }

    public async Task IngestAsync(CancellationToken cancellationToken = default)
    {
        foreach (var definition in DefaultCptRules.All)
        {
            var existingRule = await _ruleStore.GetAsync(DefaultCptRules.RuleSetKey, definition.RuleName, cancellationToken);
            if (existingRule != null)
            {
                continue;
            }

            var output = new CptCodeOutput(definition.CptCodes.ToList());
            var storedRule = await _rulesEngine.CreateRuleAsync(
                DefaultCptRules.RuleSetKey,
                definition.RuleSql,
                output,
                definition.RuleName,
                definition.Domain,
                definition.Description,
                definition.RuleSerialization,
                definition.Metadata,
                cancellationToken);

            await _ruleStore.SaveAsync(DefaultCptRules.RuleSetKey, storedRule, cancellationToken);
        }
    }
}
