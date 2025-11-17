using System.Linq;
using System.Threading.Tasks;
using AutoFixture.NUnit3;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Prosperity.Api.Infrastructure.RulesEngine;

namespace Prosperity.Api.Infrastructure.Storages.Tests.Services;

[TestFixture]
public class CptRuleIngestionServiceTests
{
    [Test, AutoData]
    public async Task IngestAsync_WhenStoreEmpty_AddsAllDefaultRules()
    {
        //Arrange
        var ingestionService = CreateService(out var engine, out var ruleStore);

        //Act
        await ingestionService.IngestAsync();
        var storedRules = await ruleStore.GetAllAsync(DefaultCptRules.RuleSetKey);
        var encounter = new Encounter("Therapy Progress Note", 45, "LCSW");
        var evaluation = await engine.EvaluateAsync(DefaultCptRules.RuleSetKey, encounter);

        //Assert
        Assert.That(storedRules.Count, Is.EqualTo(DefaultCptRules.All.Count));
        Assert.That(evaluation.HasMatches, Is.True);
        var match = evaluation.Matches.Single(m => m.RuleName == "Therapy 45 min");
        CollectionAssert.AreEquivalent(new[] { "90834", "90836" }, match.Output.CptCodes);
    }

    [Test, AutoData]
    public async Task IngestAsync_WhenRuleAlreadyExists_DoesNotOverride()
    {
        //Arrange
        var ingestionService = CreateService(out var engine, out var ruleStore);
        var existingOutput = new CptCodeOutput(["00000"]);
        await engine.CreateRuleAsync(
            DefaultCptRules.RuleSetKey,
            "(noteType = 'Group Note')",
            existingOutput,
            "Group Therapy");

        //Act
        await ingestionService.IngestAsync();
        var storedRules = await ruleStore.GetAllAsync(DefaultCptRules.RuleSetKey);
        var evaluation = await engine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Group Note", 30, "LCSW"));

        //Assert
        Assert.That(storedRules.Count, Is.EqualTo(DefaultCptRules.All.Count));
        var match = evaluation.Matches.Single(m => m.RuleName == "Group Therapy");
        CollectionAssert.AreEquivalent(new[] { "00000" }, match.Output.CptCodes);
    }

    [Test, AutoData]
    public async Task IngestAsync_WhenCalledMultipleTimes_DoesNotDuplicateRules()
    {
        //Arrange
        var ingestionService = CreateService(out var engine, out var ruleStore);

        //Act
        await ingestionService.IngestAsync();
        await ingestionService.IngestAsync();
        var storedRules = await ruleStore.GetAllAsync(DefaultCptRules.RuleSetKey);
        var evaluation = await engine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Group Note", 30, "LCSW"));

        //Assert
        Assert.That(storedRules.Count, Is.EqualTo(DefaultCptRules.All.Count));
        var matchCount = evaluation.Matches.Count(m => m.RuleName == "Group Therapy");
        Assert.That(matchCount, Is.EqualTo(1));
    }

    [Test, AutoData]
    public async Task IngestAsync_PopulatesStoredRuleMetadata()
    {
        //Arrange
        var ingestionService = CreateService(out _, out var ruleStore);

        //Act
        await ingestionService.IngestAsync();
        var storedRule = await ruleStore.GetAsync(DefaultCptRules.RuleSetKey, "Initial Intake");

        //Assert
        Assert.That(storedRule, Is.Not.Null);
        Assert.That(storedRule!.Domain, Is.EqualTo("codeAssist"));
        Assert.That(storedRule.Description, Is.EqualTo("90791: Intake performed by non-MD including LCSW/LPC/etc."));
        Assert.That(string.IsNullOrWhiteSpace(storedRule.RuleSerialization), Is.False);
        Assert.That(storedRule.Metadata, Is.Not.Null);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesInitialIntakeRule()
    {
        //Arrange
        var ingestionService = CreateService(out var engine, out _);

        //Act
        await ingestionService.IngestAsync();
        var evaluation = await engine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Intake Note", 60, "LCSW"));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Initial Intake", ["90791"]);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesPsychiatricIntakeRule()
    {
        //Arrange
        var ingestionService = CreateService(out var engine, out _);

        //Act
        await ingestionService.IngestAsync();
        var evaluation = await engine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Intake Note", 50, "MD"));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Psychiatric Intake", ["90792"]);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesTherapy30Rule()
    {
        //Arrange
        var ingestionService = CreateService(out var engine, out _);

        //Act
        await ingestionService.IngestAsync();
        var evaluation = await engine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Therapy Progress Note", 30, "LCSW"));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Therapy 30 min", ["90832", "90833"]);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesTherapy45Rule()
    {
        //Arrange
        var ingestionService = CreateService(out var engine, out _);

        //Act
        await ingestionService.IngestAsync();
        var evaluation = await engine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Therapy Progress Note", 45, "LCSW"));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Therapy 45 min", ["90834", "90836"]);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesTherapy60Rule()
    {
        //Arrange
        var ingestionService = CreateService(out var engine, out _);

        //Act
        await ingestionService.IngestAsync();
        var evaluation = await engine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Therapy Progress Note", 60, "LCSW"));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Therapy 60 min", ["90837", "90838"]);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesGroupTherapyRule()
    {
        //Arrange
        var ingestionService = CreateService(out var engine, out _);

        //Act
        await ingestionService.IngestAsync();
        var evaluation = await engine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Group Note", 30, "LCSW"));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Group Therapy", ["90853", "90849"]);
    }

    private static CptRuleIngestionService CreateService(
        out IDynamicRulesEngine<Encounter, CptCodeOutput> engine,
        out InMemoryRuleStore ruleStore)
    {
        var converter = new SqlToLinqConverter();
        var builder = new DynamicRuleBuilder(converter);
        ruleStore = new InMemoryRuleStore();
        engine = new DynamicRulesEngine<Encounter, CptCodeOutput>(builder, ruleStore);
        return new CptRuleIngestionService(engine, ruleStore);
    }

    private static void AssertDefaultRuleMatch(
        EvaluationResult<Encounter, CptCodeOutput> evaluation,
        string ruleName,
        string[] expectedCodes)
    {
        Assert.That(evaluation.HasMatches, Is.True);
        var match = evaluation.Matches.SingleOrDefault(m => m.RuleName == ruleName);
        Assert.That(match, Is.Not.Null);
        CollectionAssert.AreEquivalent(expectedCodes, match!.Output.CptCodes);
    }
}
