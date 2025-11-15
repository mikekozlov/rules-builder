using NUnit.Framework.Legacy;
using Prosperity.Api.Infrastructure.RulesEngine;

namespace Prosperity.Api.Infrastructure.Storages.Tests.Services;

public class DynamicRulesEngineTests
{
    private const string RuleSetKey = "cpt";
    private IDynamicRulesEngine<Encounter, CptCodeOutput> _engine = null!;
    private InMemoryRuleStore _ruleStore = null!;

    [SetUp]
    public async Task SetUp()
    {
        var sqlConverter = new SqlToLinqConverter();
        var ruleBuilder = new DynamicRuleBuilder(sqlConverter);
        _ruleStore = new InMemoryRuleStore();
        _engine = new DynamicRulesEngine<Encounter, CptCodeOutput>(ruleBuilder, _ruleStore);

        var condition = "(noteType = 'Intake Note' and encounterDuration >= 60 and providerCredentials in ('MD','PSY'))";
        var output = new CptCodeOutput(["90791"]);

        await _engine.CreateRuleAsync(RuleSetKey, condition, output, "Intake_90791");
    }

    [Test]
    public async Task EvaluateAsync_WhenEncounterMatchesRule_ReturnsMatchedRuleWithCptCodes()
    {
        var encounter = new Encounter("Intake Note", 75, "MD");

        var result = await _engine.EvaluateAsync(RuleSetKey, encounter);

        Assert.That(result.HasMatches, Is.True);
        Assert.That(result.Matches.Count, Is.EqualTo(1));

        var match = result.Matches.Single();
        CollectionAssert.AreEquivalent(new[] { "90791" }, match.Output.CptCodes);
        Assert.That(match.RuleName, Is.EqualTo("Intake_90791"));
    }

    [Test]
    public async Task EvaluateAsync_WhenEncounterDoesNotMatchRule_ReturnsNoMatches()
    {
        var encounter = new Encounter("Group Note", 30, "LCSW");

        var result = await _engine.EvaluateAsync(RuleSetKey, encounter);

        Assert.That(result.HasMatches, Is.False);
        Assert.That(result.Matches, Is.Empty);
    }
}
