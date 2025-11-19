using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture.NUnit3;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Prosperity.Api.Infrastructure.RulesEngine.Abstractions;
using Prosperity.Api.Infrastructure.RulesEngine.Builders;
using Prosperity.Api.Infrastructure.RulesEngine.Engine;
using Prosperity.Api.Infrastructure.RulesEngine.Ingestion;
using Prosperity.Api.Infrastructure.RulesEngine.Models;
using Prosperity.Api.Infrastructure.RulesEngine.Storage;

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
        var evaluationEngine = await CreateEvaluationEngineAsync(ruleStore);
        var encounter = new Encounter("Progress Note", 45, new[] { "LCSW" });
        var evaluation = await evaluationEngine.EvaluateAsync(DefaultCptRules.RuleSetKey, encounter);

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
        var storedRule = await engine.CreateRuleAsync(
            DefaultCptRules.RuleSetKey,
            "(noteType = 'Group Note')",
            existingOutput,
            "Group Therapy");
        await ruleStore.SaveAsync(DefaultCptRules.RuleSetKey, storedRule);

        //Act
        await ingestionService.IngestAsync();
        var storedRules = await ruleStore.GetAllAsync(DefaultCptRules.RuleSetKey);
        var evaluationEngine = await CreateEvaluationEngineAsync(ruleStore);
        var evaluation = await evaluationEngine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Group Note", 30, new[] { "LCSW" }));

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
        var evaluationEngine = await CreateEvaluationEngineAsync(ruleStore);
        var evaluation = await evaluationEngine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Group Note", 30, new[] { "LCSW" }));

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
        Assert.That(storedRule.Description, Is.EqualTo("Initial Intake"));
        Assert.That(string.IsNullOrWhiteSpace(storedRule.RuleSerialization), Is.False);
        Assert.That(storedRule.Metadata, Is.Not.Null);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesInitialIntakeRule()
    {
        //Arrange
        var ingestionService = CreateService(out _, out var ruleStore);

        //Act
        await ingestionService.IngestAsync();
        var evaluationEngine = await CreateEvaluationEngineAsync(ruleStore);
        var evaluation = await evaluationEngine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Intake Note", 60, new[] { "LCSW" }));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Initial Intake", ["90791"]);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesPsychiatricIntakeRule()
    {
        //Arrange
        var ingestionService = CreateService(out _, out var ruleStore);

        //Act
        await ingestionService.IngestAsync();
        var evaluationEngine = await CreateEvaluationEngineAsync(ruleStore);
        var evaluation = await evaluationEngine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Intake Note", 50, new[] { "MD" }));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Psychiatric Intake", ["90792"]);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesTherapy30Rule()
    {
        //Arrange
        var ingestionService = CreateService(out _, out var ruleStore);

        //Act
        await ingestionService.IngestAsync();
        var evaluationEngine = await CreateEvaluationEngineAsync(ruleStore);
        var evaluation = await evaluationEngine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Progress Note", 30, new[] { "LCSW" }));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Therapy 30 min", ["90832", "90833"]);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesTherapy45Rule()
    {
        //Arrange
        var ingestionService = CreateService(out _, out var ruleStore);

        //Act
        await ingestionService.IngestAsync();
        var evaluationEngine = await CreateEvaluationEngineAsync(ruleStore);
        var evaluation = await evaluationEngine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Progress Note", 45, new[] { "LCSW" }));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Therapy 45 min", ["90834", "90836"]);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesTherapy60Rule()
    {
        //Arrange
        var ingestionService = CreateService(out _, out var ruleStore);

        //Act
        await ingestionService.IngestAsync();
        var evaluationEngine = await CreateEvaluationEngineAsync(ruleStore);
        var evaluation = await evaluationEngine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Progress Note", 60, new[] { "LCSW" }));

        //Assert
        AssertDefaultRuleMatch(evaluation, "Therapy 60 min", ["90837", "90838"]);
    }

    [Test, AutoData]
    public async Task IngestAsync_CreatesGroupTherapyRule()
    {
        //Arrange
        var ingestionService = CreateService(out _, out var ruleStore);

        //Act
        await ingestionService.IngestAsync();
        var evaluationEngine = await CreateEvaluationEngineAsync(ruleStore);
        var evaluation = await evaluationEngine.EvaluateAsync(DefaultCptRules.RuleSetKey, new Encounter("Group Note", 30, new[] { "LCSW" }));

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
        engine = new DynamicRulesEngine<Encounter, CptCodeOutput>(builder);
        return new CptRuleIngestionService(engine, ruleStore);
    }

    private static async Task<IDynamicRulesEngine<Encounter, CptCodeOutput>> CreateEvaluationEngineAsync(IRuleStore ruleStore)
    {
        var converter = new SqlToLinqConverter();
        var builder = new DynamicRuleBuilder(converter);
        var storedRules = await ruleStore.GetAllAsync(DefaultCptRules.RuleSetKey);
        var ruleSets = new Dictionary<string, IReadOnlyCollection<StoredRule>>
        {
            { DefaultCptRules.RuleSetKey, storedRules }
        };

        return new DynamicRulesEngine<Encounter, CptCodeOutput>(builder, ruleSets);
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
