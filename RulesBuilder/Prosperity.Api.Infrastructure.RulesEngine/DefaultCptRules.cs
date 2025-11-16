namespace Prosperity.Api.Infrastructure.RulesEngine;

public sealed record CptRuleDefinition(string RuleName, string Condition, IReadOnlyCollection<string> CptCodes);

public static class DefaultCptRules
{
    public const string RuleSetKey = "cpt";

    public static IReadOnlyCollection<CptRuleDefinition> All { get; } =
    [
        new CptRuleDefinition(
            "Initial Intake",
            "(noteType = 'Intake Note' and providerCredentials in ('MD','PSYC','DO','LCSW','LPC','LMHC','LMFT','PSY') and encounterDuration >= 16 and encounterDuration <= 90)",
            ["90791"]),
        new CptRuleDefinition(
            "Psychiatric Intake",
            "(noteType = 'Intake Note' and providerCredentials in ('MD','PSYC','DO') and encounterDuration >= 16 and encounterDuration <= 90)",
            ["90792"]),
        new CptRuleDefinition(
            "Therapy 30 min",
            "(noteType = 'Therapy Progress Note' and encounterDuration >= 16 and encounterDuration <= 37)",
            ["90832", "90833"]),
        new CptRuleDefinition(
            "Therapy 45 min",
            "(noteType = 'Therapy Progress Note' and encounterDuration >= 38 and encounterDuration <= 52)",
            ["90834", "90836"]),
        new CptRuleDefinition(
            "Therapy 60 min",
            "(noteType = 'Therapy Progress Note' and encounterDuration >= 53)",
            ["90837", "90838"]),
        new CptRuleDefinition(
            "Group Therapy",
            "(noteType = 'Group Note')",
            ["90853", "90849"])
    ];
}
