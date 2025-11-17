using System;
using System.Collections.Generic;
using NUnit.Framework.Legacy;
using Prosperity.Api.Infrastructure.RulesEngine;

namespace Prosperity.Api.Infrastructure.Storages.Tests.Services;

public class CptRuleIngestionServiceTests
{
    private InMemoryRuleStore _ruleStore = null!;
    private IDynamicRulesEngine<Encounter, CptCodeOutput> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var converter = new SqlToLinqConverter();
        var builder = new DynamicRuleBuilder(converter);
        _ruleStore = new InMemoryRuleStore();
        _engine = new DynamicRulesEngine<Encounter, CptCodeOutput>(builder, _ruleStore);
    }

    [Test]
    public async Task IngestAsync_WhenStoreEmpty_AddsAllDefaultRules()
    {
        //Arrange
        var ingestionService = new CptRuleIngestionService(_engine, _ruleStore);

        //Act
        await ingestionService.IngestAsync();

        var storedRules = await _ruleStore.GetAllAsync(DefaultCptRules.RuleSetKey);
        Assert.That(storedRules.Count, Is.EqualTo(DefaultCptRules.All.Count));

        var encounter = new Encounter("Therapy Progress Note", 45, "LCSW");
        var evaluation = await _engine.EvaluateAsync(DefaultCptRules.RuleSetKey, encounter);

        //Assert
        Assert.That(evaluation.HasMatches, Is.True);
        var match = evaluation.Matches.Single(m => m.RuleName == "Therapy 45 min");
        CollectionAssert.AreEquivalent(new[] { "90834", "90836" }, match.Output.CptCodes);
    }

    [Test]
    public async Task IngestAsync_WhenRuleAlreadyExists_DoesNotOverride()
    {
        //Arrange
        var existingOutput = new CptCodeOutput(["00000"]);
        await _engine.CreateRuleAsync(
            DefaultCptRules.RuleSetKey,
            "(noteType = 'Group Note')",
            existingOutput,
            "Group Therapy");

        var ingestionService = new CptRuleIngestionService(_engine, _ruleStore);

        //Act
        await ingestionService.IngestAsync();

        var storedRules = await _ruleStore.GetAllAsync(DefaultCptRules.RuleSetKey);
        Assert.That(storedRules.Count, Is.EqualTo(DefaultCptRules.All.Count));

        var evaluation = await _engine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Group Note", 30, "LCSW"));
        //Assert
        var match = evaluation.Matches.Single(m => m.RuleName == "Group Therapy");
        CollectionAssert.AreEquivalent(new[] { "00000" }, match.Output.CptCodes);
    }

    [TestCaseSource(nameof(DefaultRuleCoverageTestCases))]
    public async Task IngestAsync_CreatesRuleForEachDefaultDefinition(string ruleName, Func<Encounter> encounterFactory, string[] expectedCodes)
    {
        //Arrange
        var ingestionService = new CptRuleIngestionService(_engine, _ruleStore);

        //Act
        await ingestionService.IngestAsync();
        var encounter = encounterFactory();
        var evaluation = await _engine.EvaluateAsync(DefaultCptRules.RuleSetKey, encounter);

        //Assert
        Assert.That(evaluation.HasMatches, Is.True);
        var match = evaluation.Matches.SingleOrDefault(m => m.RuleName == ruleName);
        Assert.That(match, Is.Not.Null);
        CollectionAssert.AreEquivalent(expectedCodes, match!.Output.CptCodes);
    }

    [Test]
    public async Task IngestAsync_WhenCalledMultipleTimes_DoesNotDuplicateRules()
    {
        //Arrange
        var ingestionService = new CptRuleIngestionService(_engine, _ruleStore);

        //Act
        await ingestionService.IngestAsync();
        await ingestionService.IngestAsync();
        var storedRules = await _ruleStore.GetAllAsync(DefaultCptRules.RuleSetKey);
        var evaluation = await _engine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Group Note", 30, "LCSW"));

        //Assert
        Assert.That(storedRules.Count, Is.EqualTo(DefaultCptRules.All.Count));
        var matchCount = evaluation.Matches.Count(m => m.RuleName == "Group Therapy");
        Assert.That(matchCount, Is.EqualTo(1));
    }

    [Test]
    public async Task IngestAsync_PopulatesStoredRuleMetadata()
    {
        //Arrange
        var ingestionService = new CptRuleIngestionService(_engine, _ruleStore);

        //Act
        await ingestionService.IngestAsync();
        var storedRule = await _ruleStore.GetAsync(DefaultCptRules.RuleSetKey, "Initial Intake");

        //Assert
        Assert.That(storedRule, Is.Not.Null);
        Assert.That(storedRule!.Domain, Is.EqualTo("codeAssist"));
        Assert.That(storedRule.Description, Is.EqualTo("90791: Intake performed by non-MD including LCSW/LPC/etc."));
        Assert.That(string.IsNullOrWhiteSpace(storedRule.RuleSerialization), Is.False);
        Assert.That(storedRule.Metadata, Is.Not.Null);
    }

    private static IEnumerable<TestCaseData> DefaultRuleCoverageTestCases()
    {
        yield return new TestCaseData(
                "Initial Intake",
                new Func<Encounter>(() => new Encounter("Intake Note", 60, "LCSW")),
                new[] { "90791" })
            .SetName("IngestAsync_CreatesRuleForEachDefaultDefinition_InitialIntake");

        yield return new TestCaseData(
                "Psychiatric Intake",
                new Func<Encounter>(() => new Encounter("Intake Note", 50, "MD")),
                new[] { "90792" })
            .SetName("IngestAsync_CreatesRuleForEachDefaultDefinition_PsychiatricIntake");

        yield return new TestCaseData(
                "Therapy 30 min",
                new Func<Encounter>(() => new Encounter("Therapy Progress Note", 30, "LCSW")),
                new[] { "90832", "90833" })
            .SetName("IngestAsync_CreatesRuleForEachDefaultDefinition_Therapy30");

        yield return new TestCaseData(
                "Therapy 45 min",
                new Func<Encounter>(() => new Encounter("Therapy Progress Note", 45, "LCSW")),
                new[] { "90834", "90836" })
            .SetName("IngestAsync_CreatesRuleForEachDefaultDefinition_Therapy45");

        yield return new TestCaseData(
                "Therapy 60 min",
                new Func<Encounter>(() => new Encounter("Therapy Progress Note", 60, "LCSW")),
                new[] { "90837", "90838" })
            .SetName("IngestAsync_CreatesRuleForEachDefaultDefinition_Therapy60");

        yield return new TestCaseData(
                "Group Therapy",
                new Func<Encounter>(() => new Encounter("Group Note", 30, "LCSW")),
                new[] { "90853", "90849" })
            .SetName("IngestAsync_CreatesRuleForEachDefaultDefinition_GroupTherapy");
    }
}
