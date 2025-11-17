using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NRules;
using NRules.Json;
using NRules.RuleModel;
using Prosperity.Api.Infrastructure.RulesEngine.Abstractions;
using Prosperity.Api.Infrastructure.RulesEngine.Models;

namespace Prosperity.Api.Infrastructure.RulesEngine.Engine;

public sealed class DynamicRulesEngine<TFact, TOutput> : IDynamicRulesEngine<TFact, TOutput>
{
    private readonly IDynamicRuleBuilder _ruleBuilder;
    private readonly JsonSerializerOptions _ruleSerializerOptions;
    private readonly JsonSerializerOptions _outputSerializerOptions;
    private readonly Dictionary<string, List<StoredRule>> _ruleSets;
    private readonly object _ruleSetsSync;

    public DynamicRulesEngine(IDynamicRuleBuilder ruleBuilder)
        : this(
            ruleBuilder,
            null,
            CreateDefaultRuleSerializerOptions(),
            CreateDefaultOutputSerializerOptions())
    {
    }

    public DynamicRulesEngine(
        IDynamicRuleBuilder ruleBuilder,
        IReadOnlyDictionary<string, IReadOnlyCollection<StoredRule>>? initialRuleSets)
        : this(
            ruleBuilder,
            initialRuleSets,
            CreateDefaultRuleSerializerOptions(),
            CreateDefaultOutputSerializerOptions())
    {
    }

    public DynamicRulesEngine(
        IDynamicRuleBuilder ruleBuilder,
        IReadOnlyDictionary<string, IReadOnlyCollection<StoredRule>>? initialRuleSets,
        JsonSerializerOptions ruleSerializerOptions,
        JsonSerializerOptions outputSerializerOptions)
    {
        _ruleBuilder = ruleBuilder;
        _ruleSerializerOptions = new JsonSerializerOptions(ruleSerializerOptions);
        _outputSerializerOptions = new JsonSerializerOptions(outputSerializerOptions);
        _ruleSets = new Dictionary<string, List<StoredRule>>(StringComparer.Ordinal);
        _ruleSetsSync = new object();

        if (initialRuleSets != null)
        {
            foreach (var (ruleSetKey, storedRules) in initialRuleSets)
            {
                _ruleSets[ruleSetKey] = storedRules?.ToList() ?? new List<StoredRule>();
            }
        }

        RuleSerializer.Setup(_ruleSerializerOptions);
    }

    public Task<StoredRule> CreateRuleAsync(
        string ruleSetKey,
        string condition,
        TOutput outputTemplate,
        string? ruleName = null,
        string? domain = null,
        string? description = null,
        string? ruleSerialization = null,
        RuleMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ruleDefinition = _ruleBuilder.BuildRule<TFact>(condition, outputTemplate!, ruleName);
        var effectiveName = string.IsNullOrWhiteSpace(ruleDefinition.Name)
            ? Guid.NewGuid().ToString("N")
            : ruleDefinition.Name;

        var ruleJson = JsonSerializer.Serialize(ruleDefinition, _ruleSerializerOptions);
        var outputJson = JsonSerializer.Serialize(outputTemplate, _outputSerializerOptions);

        var storedRule = new StoredRule(
            effectiveName,
            condition,
            ruleJson,
            outputJson,
            domain,
            description,
            ruleSerialization,
            metadata);

        AddOrUpdateRule(ruleSetKey, storedRule);

        return Task.FromResult(storedRule);
    }

    public Task<EvaluationResult<TFact, TOutput>> EvaluateAsync(
        string ruleSetKey,
        TFact fact,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var storedRules = GetRuleSnapshot(ruleSetKey);
        if (storedRules.Count == 0)
        {
            return Task.FromResult(new EvaluationResult<TFact, TOutput>(fact, []));
        }

        var ruleDefinitions = storedRules
            .Select(storedRule => JsonSerializer.Deserialize<IRuleDefinition>(storedRule.RuleJson, _ruleSerializerOptions))
            .OfType<IRuleDefinition>()
            .ToList();

        if (ruleDefinitions.Count == 0)
        {
            return Task.FromResult(new EvaluationResult<TFact, TOutput>(fact, []));
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

        if (firedRuleNames.Count != 0)
        {
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
        }

        return Task.FromResult(new EvaluationResult<TFact, TOutput>(fact, matches));
    }

    private IReadOnlyList<StoredRule> GetRuleSnapshot(string ruleSetKey)
    {
        lock (_ruleSetsSync)
        {
            if (_ruleSets.TryGetValue(ruleSetKey, out var rules) && rules.Count > 0)
            {
                return rules.ToArray();
            }
        }

        return Array.Empty<StoredRule>();
    }

    private void AddOrUpdateRule(string ruleSetKey, StoredRule rule)
    {
        lock (_ruleSetsSync)
        {
            if (!_ruleSets.TryGetValue(ruleSetKey, out var rules))
            {
                rules = new List<StoredRule>();
                _ruleSets[ruleSetKey] = rules;
            }

            var existingIndex = rules.FindIndex(r => string.Equals(r.Name, rule.Name, StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                rules[existingIndex] = rule;
            }
            else
            {
                rules.Add(rule);
            }
        }
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
