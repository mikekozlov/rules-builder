using System.Linq;
using System.Linq.Expressions;
using AutoFixture;
using AutoFixture.NUnit3;
using FluentAssertions;
using Moq;
using NRules.RuleModel;
using NUnit.Framework;
using Prosperity.Api.Infrastructure.RulesEngine.Abstractions;
using Prosperity.Api.Infrastructure.RulesEngine.Builders;

namespace Prosperity.Api.Infrastructure.Storages.Tests;

[TestFixture]
public class DynamicRuleBuilderTests
{
    [Test, AutoMoqData]
    public void BuildRule_ShouldCreatePatternAndInsertAction(
        [Frozen] Mock<ISqlToLinqConverter> sqlConverter,
        IFixture fixture,
        DynamicRuleBuilder sut)
    {
        // Arrange
        var condition = "duration > 45";
        var output = fixture.Create<object>();
        Expression<Func<SampleFact, bool>> predicate = fact => fact.Duration > 45;
        sqlConverter.Setup(converter => converter.ConvertToExpression<SampleFact>(condition)).Returns(predicate);

        // Act
        var definition = sut.BuildRule<SampleFact>(condition, output, "custom-name");

        // Assert
        definition.Name.Should().Be("custom-name");
        sqlConverter.Verify(converter => converter.ConvertToExpression<SampleFact>(condition), Times.Once);
        var pattern = definition.LeftHandSide.ChildElements.OfType<PatternElement>().Single();
        pattern.ValueType.Should().Be(typeof(SampleFact));
        var expressionElement = pattern.Expressions.Should().ContainSingle().Subject;
        expressionElement.Expression.Should().BeSameAs(predicate);
        var action = definition.RightHandSide.Actions.Should().ContainSingle().Subject;
        var compiled = ((Expression<Action<IContext>>)action.Expression).Compile();
        var context = new Mock<IContext>();
        compiled(context.Object);
        context.Verify(ctx => ctx.Insert(output), Times.Once);
    }

    [Test, AutoMoqData]
    public void BuildRule_ShouldUseDefaultNameWhenNameMissing(
        [Frozen] Mock<ISqlToLinqConverter> sqlConverter,
        IFixture fixture,
        DynamicRuleBuilder sut)
    {
        // Arrange
        var condition = "noteType = 'Initial'";
        var output = fixture.Create<object>();
        Expression<Func<SampleFact, bool>> predicate = fact => fact.Duration == 60;
        sqlConverter.Setup(converter => converter.ConvertToExpression<SampleFact>(condition)).Returns(predicate);

        // Act
        var definition = sut.BuildRule<SampleFact>(condition, output);

        // Assert
        definition.Name.Should().Be($"Dynamic Rule: {condition}");
    }

    [Test, AutoMoqData]
    public void BuildRule_WithRuntimeType_ShouldDelegateToGenericVersion(
        [Frozen] Mock<ISqlToLinqConverter> sqlConverter,
        IFixture fixture,
        DynamicRuleBuilder sut)
    {
        // Arrange
        var condition = "duration >= 90";
        var output = fixture.Create<object>();
        Expression<Func<SampleFact, bool>> predicate = fact => fact.Duration >= 90;
        sqlConverter.Setup(converter => converter.ConvertToExpression<SampleFact>(condition)).Returns(predicate);

        // Act
        var definition = sut.BuildRule(typeof(SampleFact), condition, output, null);

        // Assert
        sqlConverter.Verify(converter => converter.ConvertToExpression<SampleFact>(condition), Times.Once);
        definition.LeftHandSide.ChildElements.OfType<PatternElement>().Single().ValueType.Should().Be(typeof(SampleFact));
    }

    private sealed record SampleFact(string NoteType, int Duration);
}
