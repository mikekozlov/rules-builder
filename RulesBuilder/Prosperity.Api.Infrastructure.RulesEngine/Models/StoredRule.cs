namespace Prosperity.Api.Infrastructure.RulesEngine.Models;

public sealed class StoredRule
{
    public StoredRule(
        string name,
        string condition,
        string ruleJson,
        string? outputJson,
        string? domain = null,
        string? description = null,
        string? ruleSerialization = null,
        RuleMetadata? metadata = null)
    {
        Name = name;
        Condition = condition;
        RuleJson = ruleJson;
        OutputJson = outputJson;
        Domain = domain;
        Description = description;
        RuleSerialization = ruleSerialization;
        Metadata = metadata;
    }

    public string Name { get; }
    public string Condition { get; }
    public string RuleJson { get; }
    public string? OutputJson { get; }
    public string? Domain { get; }
    public string? Description { get; }
    public string? RuleSerialization { get; }
    public RuleMetadata? Metadata { get; }
}
