using System.Text.Json;
using NRules;
using NRules.Json;
using NRules.RuleModel;

namespace Prosperity.Api.Infrastructure.RulesEngine;

public sealed class DynamicRulesEngine<TFact, TOutput> : IDynamicRulesEngine<TFact, TOutput>
{
    private readonly DynamicRuleBuilder _ruleBuilder;
    private readonly IRuleStore _ruleStore;
    private readonly JsonSerializerOptions _ruleSerializerOptions;
    private readonly JsonSerializerOptions _outputSerializerOptions;

    public DynamicRulesEngine(DynamicRuleBuilder ruleBuilder, IRuleStore ruleStore)
        : this(
            ruleBuilder,
            ruleStore,
            CreateDefaultRuleSerializerOptions(),
            CreateDefaultOutputSerializerOptions())
    {
    }

    public DynamicRulesEngine(
        DynamicRuleBuilder ruleBuilder,
        IRuleStore ruleStore,
        JsonSerializerOptions ruleSerializerOptions,
        JsonSerializerOptions outputSerializerOptions)
    {
        _ruleBuilder = ruleBuilder;
        _ruleStore = ruleStore;

        _ruleSerializerOptions = new JsonSerializerOptions(ruleSerializerOptions);
        _outputSerializerOptions = new JsonSerializerOptions(outputSerializerOptions);

        RuleSerializer.Setup(_ruleSerializerOptions);
    }

    public async Task<StoredRule> CreateRuleAsync(
        string ruleSetKey,
        string condition,
        TOutput outputTemplate,
        string? ruleName = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ruleDefinition = _ruleBuilder.BuildRule<TFact>(condition, outputTemplate!, ruleName);
        var effectiveName = string.IsNullOrWhiteSpace(ruleDefinition.Name)
            ? Guid.NewGuid().ToString("N")
            : ruleDefinition.Name;

        var ruleJson = JsonSerializer.Serialize(ruleDefinition, _ruleSerializerOptions);
        var outputJson = JsonSerializer.Serialize(outputTemplate, _outputSerializerOptions);

        var storedRule = new StoredRule(effectiveName, condition, ruleJson, outputJson);

        await _ruleStore.SaveAsync(ruleSetKey, storedRule, cancellationToken);

        return storedRule;
    }

    public async Task<EvaluationResult<TFact, TOutput>> EvaluateAsync(
        string ruleSetKey,
        TFact fact,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var storedRules = await _ruleStore.GetAllAsync(ruleSetKey, cancellationToken);
        if (storedRules.Count == 0)
        {
            return new EvaluationResult<TFact, TOutput>(fact, []);
        }

        var ruleDefinitions = storedRules.Select(storedRule => JsonSerializer.Deserialize<IRuleDefinition>(storedRule.RuleJson, _ruleSerializerOptions)).OfType<IRuleDefinition>().ToList();

        if (ruleDefinitions.Count == 0)
        {
            return new EvaluationResult<TFact, TOutput>(fact, []);
        }

        var compiler = new RuleCompiler();
        var factory = compiler.Compile(ruleDefinitions);
        var session = factory.CreateSession();

        var firedRuleNames = new HashSet<string>(StringComparer.Ordinal);
        session.Events.RuleFiredEvent += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Rule.Name))
            {
                firedRuleNames.Add(args.Rule.Name);
            }
        };

        session.Insert(fact!);
        session.Fire();

        var matches = new List<RuleMatch<TOutput>>();

        if (firedRuleNames.Count == 0)
        {
            return new EvaluationResult<TFact, TOutput>(fact, matches);
        }

        foreach (var ruleName in firedRuleNames)
        {
            var storedRule = storedRules.FirstOrDefault(r => string.Equals(r.Name, ruleName, StringComparison.Ordinal));
            if (storedRule == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(storedRule.OutputJson))
            {
                continue;
            }

            TOutput? output;

            try
            {
                output = JsonSerializer.Deserialize<TOutput>(storedRule.OutputJson, _outputSerializerOptions);
            }
            catch
            {
                continue;
            }

            if (output is null)
            {
                continue;
            }

            matches.Add(new RuleMatch<TOutput>(ruleName, output));
        }

        return new EvaluationResult<TFact, TOutput>(fact, matches);
    }

    private static JsonSerializerOptions CreateDefaultRuleSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null
        };
    }

    private static JsonSerializerOptions CreateDefaultOutputSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
