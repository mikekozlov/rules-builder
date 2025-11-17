using System.Linq;
using NUnit.Framework;
using Prosperity.Api.Infrastructure.RulesEngine;

namespace Prosperity.Api.Infrastructure.Storages.Tests;

public class DefaultCptRulesTests
{
    [Test]
    public void All_ShouldExposeDescriptionsForEveryRule()
    {
        //Arrange
        var rules = DefaultCptRules.All;

        //Act
        var initialIntake = rules.Single(rule => rule.RuleName == "Initial Intake");

        //Assert
        Assert.That(rules.All(rule => !string.IsNullOrWhiteSpace(rule.Description)), Is.True);
        Assert.That(initialIntake.Description, Is.EqualTo("90791: Intake performed by non-MD including LCSW/LPC/etc."));
    }

    [Test]
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
