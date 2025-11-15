namespace Prosperity.Api.Infrastructure.RulesEngine;

public sealed class RuleMatch<TOutput>
{
    public RuleMatch(string ruleName, TOutput output)
    {
        RuleName = ruleName;
        Output = output;
    }

    public string RuleName { get; }

    public TOutput Output { get; }
}
