using System;
using System.Linq.Expressions;
using AutoFixture.NUnit3;
using FluentAssertions;
using NUnit.Framework;
using Prosperity.Api.Infrastructure.RulesEngine.Builders;

namespace Prosperity.Api.Infrastructure.Storages.Tests;

[TestFixture]
public class SqlToLinqConverterTests
{
    [Test, AutoData]
    public void ConvertToExpression_WithStartsWithPattern_ShouldMatchPrefix(SqlToLinqConverter sut)
    {
        // Arrange
        var clause = "Name like 'Jo%'";

        // Act
        var predicate = sut.ConvertToExpression<TestEntity>(clause);

        // Assert
        var compiled = predicate.Compile();
        compiled(new TestEntity { Name = "John" }).Should().BeTrue();
        compiled(new TestEntity { Name = "Alice" }).Should().BeFalse();
    }

    [Test, AutoData]
    public void ConvertToExpression_WithContainsPattern_ShouldMatchSubstring(SqlToLinqConverter sut)
    {
        // Arrange
        var clause = "Name like '%mith%'";

        // Act
        var predicate = sut.ConvertToExpression<TestEntity>(clause);

        // Assert
        var compiled = predicate.Compile();
        compiled(new TestEntity { Name = "Smith" }).Should().BeTrue();
        compiled(new TestEntity { Name = "Smoth" }).Should().BeFalse();
    }

    [Test, AutoData]
    public void ConvertToExpression_WithInOperator_ShouldMatchAnyCandidate(SqlToLinqConverter sut)
    {
        // Arrange
        var clause = "Score in ('1','3','5')";

        // Act
        var predicate = sut.ConvertToExpression<TestEntity>(clause);

        // Assert
        var compiled = predicate.Compile();
        compiled(new TestEntity { Score = 3 }).Should().BeTrue();
        compiled(new TestEntity { Score = 4 }).Should().BeFalse();
    }

    [Test, AutoData]
    public void ConvertToExpression_WithNotInOperator_ShouldExcludeCandidates(SqlToLinqConverter sut)
    {
        // Arrange
        var clause = "Score not in ('1','2')";

        // Act
        var predicate = sut.ConvertToExpression<TestEntity>(clause);

        // Assert
        var compiled = predicate.Compile();
        compiled(new TestEntity { Score = 5 }).Should().BeTrue();
        compiled(new TestEntity { Score = 1 }).Should().BeFalse();
    }

    [Test, AutoData]
    public void ConvertToExpression_WithNullableComparison_ShouldHandleNullChecks(SqlToLinqConverter sut)
    {
        // Arrange
        var clause = "Name is null";

        // Act
        var predicate = sut.ConvertToExpression<TestEntity>(clause);

        // Assert
        var compiled = predicate.Compile();
        compiled(new TestEntity { Name = null }).Should().BeTrue();
        compiled(new TestEntity { Name = "value" }).Should().BeFalse();
    }

    [Test, AutoData]
    public void ConvertToExpression_WhenNonNullableComparedToNull_ShouldThrow(SqlToLinqConverter sut)
    {
        // Arrange
        var clause = "Score is null";

        // Act
        var action = () => sut.ConvertToExpression<TestEntity>(clause);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be compared to NULL*");
    }

    [Test, AutoData]
    public void ConvertToExpression_WhenLikeUsedOnNonString_ShouldThrow(SqlToLinqConverter sut)
    {
        // Arrange
        var clause = "Score like '1%'";

        // Act
        var action = () => sut.ConvertToExpression<TestEntity>(clause);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*LIKE can only be used with string properties*");
    }

    private sealed class TestEntity
    {
        public string? Name { get; set; } = string.Empty;
        public int Score { get; set; }
        public int? OptionalScore { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
