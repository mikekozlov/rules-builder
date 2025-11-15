namespace Prosperity.Api.Infrastructure.RulesEngine;

public sealed class StoredRule(string name, string condition, string ruleJson, string? outputJson)
{
    public string Name { get; } = name;
    public string Condition { get; } = condition;
    public string RuleJson { get; } = ruleJson;
    public string? OutputJson { get; } = outputJson;
}
