using System.Linq;
using System.Threading.Tasks;
using AutoFixture.NUnit3;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Prosperity.Api.Infrastructure.RulesEngine;

namespace Prosperity.Api.Infrastructure.Storages.Tests.Services;

[TestFixture]
public class DynamicRulesEngineTests
{
    private const string RuleSetKey = "cpt";

    [Test, AutoData]
    public async Task EvaluateAsync_WhenEncounterMatchesRule_ReturnsMatchedRuleWithCptCodes()
    {
        //Arrange
        var engine = await CreateEngineAsync();
        var encounter = new Encounter("Intake Note", 75, "MD");

        //Act
        var result = await engine.EvaluateAsync(RuleSetKey, encounter);

        //Assert
        Assert.That(result.HasMatches, Is.True);
        Assert.That(result.Matches.Count, Is.EqualTo(1));
        var match = result.Matches.Single();
        CollectionAssert.AreEquivalent(new[] { "90791" }, match.Output.CptCodes);
        Assert.That(match.RuleName, Is.EqualTo("Intake_90791"));
    }

    [Test, AutoData]
    public async Task EvaluateAsync_WhenEncounterDoesNotMatchRule_ReturnsNoMatches()
    {
        //Arrange
        var engine = await CreateEngineAsync();
        var encounter = new Encounter("Group Note", 30, "LCSW");

        //Act
        var result = await engine.EvaluateAsync(RuleSetKey, encounter);

        //Assert
        Assert.That(result.HasMatches, Is.False);
        Assert.That(result.Matches, Is.Empty);
    }

    private static async Task<IDynamicRulesEngine<Encounter, CptCodeOutput>> CreateEngineAsync()
    {
        var sqlConverter = new SqlToLinqConverter();
        var ruleBuilder = new DynamicRuleBuilder(sqlConverter);
        var ruleStore = new InMemoryRuleStore();
        var engine = new DynamicRulesEngine<Encounter, CptCodeOutput>(ruleBuilder, ruleStore);

        var condition = "(noteType = 'Intake Note' and encounterDuration >= 60 and providerCredentials in ('MD','PSY'))";
        var output = new CptCodeOutput(["90791"]);
        await engine.CreateRuleAsync(RuleSetKey, condition, output, "Intake_90791");

        return engine;
    }
}
