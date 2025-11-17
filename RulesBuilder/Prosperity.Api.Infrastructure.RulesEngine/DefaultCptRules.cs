using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Prosperity.Api.Infrastructure.RulesEngine;

public sealed record CptRuleDefinition(
    string Domain,
    string RuleName,
    string Description,
    string RuleSql,
    string RuleSerialization,
    RuleMetadata Metadata,
    IReadOnlyCollection<string> CptCodes);

public static class DefaultCptRules
{
    public const string RuleSetKey = "cpt";
    private const string DefaultDomain = "codeAssist";
    private static readonly RuleContact DefaultAuthor = new("Ryan Fisch", "rfisch@prosperityehr.com");
    private static readonly RuleVersion DefaultVersion = new(1, 0, 0, "breaking", "Initial creation");

    public static IReadOnlyCollection<CptRuleDefinition> All { get; } =
    [
        CreateDefinition(
            "Initial Intake",
            "90791: Intake performed by non-MD including LCSW/LPC/etc.",
            "(noteType = 'Intake Note' and providerCredentials in ('MD','PSYC','DO','LCSW','LPC','LMHC','LMFT','PSY') and encounterDuration >= 16 and encounterDuration <= 90)",
            "Intake90791",
            10,
            new[]
            {
                "f => f.NoteType == \"Intake Note\"",
                "f => f.EncounterDuration >= 16 && f.EncounterDuration <= 90",
                "f => new[]{\"MD\",\"PSYC\",\"DO\",\"LCSW\",\"LPC\",\"LMHC\",\"LMFT\",\"PSY\"}.Contains(f.ProviderCredentials)"
            },
            new[] { "ctx => ctx.Insert(new CptFact(\"90791\"))" },
            CreateUtc(2025, 11, 13, 12, 0),
            "High",
            ["90791"]),
        CreateDefinition(
            "Psychiatric Intake",
            "90792: Intake performed by MD/DO/PSYC only.",
            "(noteType = 'Intake Note' and providerCredentials in ('MD','PSYC','DO') and encounterDuration >= 16 and encounterDuration <= 90)",
            "Intake90792",
            10,
            new[]
            {
                "f => f.NoteType == \"Intake Note\"",
                "f => f.EncounterDuration >= 16 && f.EncounterDuration <= 90",
                "f => new[]{\"MD\",\"PSYC\",\"DO\"}.Contains(f.ProviderCredentials)"
            },
            new[] { "ctx => ctx.Insert(new CptFact(\"90792\"))" },
            CreateUtc(2025, 11, 13, 12, 5),
            "High",
            ["90792"]),
        CreateDefinition(
            "Therapy 30 min",
            "90832/90833 short therapy duration.",
            "(noteType = 'Therapy Progress Note' and encounterDuration >= 16 and encounterDuration <= 37)",
            "TherapyShort",
            20,
            new[]
            {
                "f => f.NoteType == \"Therapy Progress Note\"",
                "f => f.EncounterDuration >= 16 && f.EncounterDuration <= 37"
            },
            new[]
            {
                "ctx => ctx.Insert(new CptFact(\"90832\"))",
                "ctx => ctx.Insert(new CptFact(\"90833\"))"
            },
            CreateUtc(2025, 11, 13, 12, 10),
            "Medium",
            ["90832", "90833"]),
        CreateDefinition(
            "Therapy 45 min",
            "90834/90836 mid-range therapy.",
            "(noteType = 'Therapy Progress Note' and encounterDuration >= 38 and encounterDuration <= 52)",
            "TherapyMid",
            20,
            new[]
            {
                "f => f.NoteType == \"Therapy Progress Note\"",
                "f => f.EncounterDuration >= 38 && f.EncounterDuration <= 52"
            },
            new[]
            {
                "ctx => ctx.Insert(new CptFact(\"90834\"))",
                "ctx => ctx.Insert(new CptFact(\"90836\"))"
            },
            CreateUtc(2025, 11, 13, 13, 0),
            "Medium",
            ["90834", "90836"]),
        CreateDefinition(
            "Therapy 60 min",
            "90837/90838 long-duration therapy.",
            "(noteType = 'Therapy Progress Note' and encounterDuration >= 53)",
            "TherapyLong",
            20,
            new[]
            {
                "f => f.NoteType == \"Therapy Progress Note\"",
                "f => f.EncounterDuration >= 53"
            },
            new[]
            {
                "ctx => ctx.Insert(new CptFact(\"90837\"))",
                "ctx => ctx.Insert(new CptFact(\"90838\"))"
            },
            CreateUtc(2025, 11, 13, 13, 10),
            "High",
            ["90837", "90838"]),
        CreateDefinition(
            "Group Therapy",
            "Group therapy CPT mapping.",
            "(noteType = 'Group Note')",
            "GroupCodes",
            30,
            new[] { "f => f.NoteType == \"Group Note\"" },
            new[]
            {
                "ctx => ctx.Insert(new CptFact(\"90853\"))",
                "ctx => ctx.Insert(new CptFact(\"90849\"))"
            },
            CreateUtc(2025, 11, 13, 14, 0),
            "Medium",
            ["90853", "90849"])
    ];

    private static CptRuleDefinition CreateDefinition(
        string ruleName,
        string description,
        string ruleSql,
        string serializationName,
        int priority,
        IReadOnlyCollection<string> conditions,
        IReadOnlyCollection<string> actions,
        DateTime timestamp,
        string severity,
        IReadOnlyCollection<string> cptCodes)
    {
        return new CptRuleDefinition(
            DefaultDomain,
            ruleName,
            description,
            ruleSql,
            BuildRuleSerialization(serializationName, description, priority, conditions, actions),
            CreateMetadata(timestamp, severity),
            cptCodes);
    }

    private static RuleMetadata CreateMetadata(DateTime timestamp, string severity)
    {
        return new RuleMetadata(
            DefaultAuthor,
            timestamp,
            DefaultAuthor,
            timestamp,
            DefaultVersion,
            null,
            new[]
            {
                new RuleTag("domain", "ClinicalDocumentation"),
                new RuleTag("function", "Validation"),
                new RuleTag("severity", severity),
                new RuleTag("lifecycle", "Production")
            });
    }

    private static string BuildRuleSerialization(
        string name,
        string description,
        int priority,
        IReadOnlyCollection<string> conditions,
        IReadOnlyCollection<string> actions)
    {
        var payload = new
        {
            Rule = new
            {
                Name = name,
                Description = description,
                Priority = priority,
                Conditions = conditions,
                Actions = actions
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    private static DateTime CreateUtc(int year, int month, int day, int hour, int minute)
    {
        return DateTime.SpecifyKind(new DateTime(year, month, day, hour, minute, 0), DateTimeKind.Utc);
    }
}
