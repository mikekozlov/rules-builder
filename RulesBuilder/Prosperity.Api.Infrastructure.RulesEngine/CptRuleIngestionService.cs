using System.Linq;

namespace Prosperity.Api.Infrastructure.RulesEngine;

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
            await _rulesEngine.CreateRuleAsync(
                DefaultCptRules.RuleSetKey,
                definition.RuleSql,
                output,
                definition.RuleName,
                definition.Domain,
                definition.Description,
                definition.RuleSerialization,
                definition.Metadata,
                cancellationToken);
        }
    }
}
