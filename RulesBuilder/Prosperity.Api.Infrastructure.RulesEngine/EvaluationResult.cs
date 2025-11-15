namespace Prosperity.Api.Infrastructure.RulesEngine;

public sealed class EvaluationResult<TFact, TOutput>
{
    public EvaluationResult(TFact fact, IReadOnlyCollection<RuleMatch<TOutput>> matches)
    {
        Fact = fact;
        Matches = matches;
    }

    public TFact Fact { get; }

    public IReadOnlyCollection<RuleMatch<TOutput>> Matches { get; }

    public bool HasMatches => Matches.Count > 0;
}
