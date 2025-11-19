using System.Linq;
using AutoFixture.NUnit3;
using NUnit.Framework;
using Prosperity.Api.Infrastructure.RulesEngine.Ingestion;

namespace Prosperity.Api.Infrastructure.Storages.Tests;

[TestFixture]
public class DefaultCptRulesTests
{
    [Test, AutoData]
    public void All_ShouldExposeDescriptionsForEveryRule()
    {
        //Arrange
        var rules = DefaultCptRules.All;

        //Act
        var initialIntake = rules.Single(rule => rule.RuleName == "Initial Intake");

        //Assert
        Assert.That(rules.All(rule => !string.IsNullOrWhiteSpace(rule.Description)), Is.True);
        Assert.That(initialIntake.Description, Is.EqualTo("Initial Intake"));
    }

    [Test, AutoData]
    public void All_ShouldProvideMetadataAndSerialization()
    {
        //Arrange
        var rules = DefaultCptRules.All;

        //Act
        var missingMetadata = rules.Where(rule => rule.Metadata is null || string.IsNullOrWhiteSpace(rule.RuleSerialization)).ToList();

        //Assert
        Assert.That(missingMetadata, Is.Empty);
    }
}
