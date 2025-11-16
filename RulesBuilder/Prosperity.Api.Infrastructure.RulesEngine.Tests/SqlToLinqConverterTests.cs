using System;
using System.Linq.Expressions;
using FluentAssertions;
using NUnit.Framework;
using Prosperity.Api.Infrastructure.RulesEngine;

namespace Prosperity.Api.Infrastructure.Storages.Tests;

[TestFixture]
public class SqlToLinqConverterTests
{
    [Test]
    public void ConvertToExpression_WithStartsWithPattern_ShouldMatchPrefix()
    {
        // Arrange
        var converter = new SqlToLinqConverter();
        var clause = "Name like 'Jo%'";

        // Act
        var predicate = converter.ConvertToExpression<TestEntity>(clause);

        // Assert
        var compiled = predicate.Compile();
        compiled(new TestEntity { Name = "John" }).Should().BeTrue();
        compiled(new TestEntity { Name = "Alice" }).Should().BeFalse();
    }

    [Test]
    public void ConvertToExpression_WithContainsPattern_ShouldMatchSubstring()
    {
        // Arrange
        var converter = new SqlToLinqConverter();
        var clause = "Name like '%mith%'";

        // Act
        var predicate = converter.ConvertToExpression<TestEntity>(clause);

        // Assert
        var compiled = predicate.Compile();
        compiled(new TestEntity { Name = "Smith" }).Should().BeTrue();
        compiled(new TestEntity { Name = "Smoth" }).Should().BeFalse();
    }

    [Test]
    public void ConvertToExpression_WithInOperator_ShouldMatchAnyCandidate()
    {
        // Arrange
        var converter = new SqlToLinqConverter();
        var clause = "Score in ('1','3','5')";

        // Act
        var predicate = converter.ConvertToExpression<TestEntity>(clause);

        // Assert
        var compiled = predicate.Compile();
        compiled(new TestEntity { Score = 3 }).Should().BeTrue();
        compiled(new TestEntity { Score = 4 }).Should().BeFalse();
    }

    [Test]
    public void ConvertToExpression_WithNotInOperator_ShouldExcludeCandidates()
    {
        // Arrange
        var converter = new SqlToLinqConverter();
        var clause = "Score not in ('1','2')";

        // Act
        var predicate = converter.ConvertToExpression<TestEntity>(clause);

        // Assert
        var compiled = predicate.Compile();
        compiled(new TestEntity { Score = 5 }).Should().BeTrue();
        compiled(new TestEntity { Score = 1 }).Should().BeFalse();
    }

    [Test]
    public void ConvertToExpression_WithNullableComparison_ShouldHandleNullChecks()
    {
        // Arrange
        var converter = new SqlToLinqConverter();
        var clause = "Name is null";

        // Act
        var predicate = converter.ConvertToExpression<TestEntity>(clause);

        // Assert
        var compiled = predicate.Compile();
        compiled(new TestEntity { Name = null }).Should().BeTrue();
        compiled(new TestEntity { Name = "value" }).Should().BeFalse();
    }

    [Test]
    public void ConvertToExpression_WhenNonNullableComparedToNull_ShouldThrow()
    {
        // Arrange
        var converter = new SqlToLinqConverter();
        var clause = "Score is null";

        // Act
        var action = () => converter.ConvertToExpression<TestEntity>(clause);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be compared to NULL*");
    }

    [Test]
    public void ConvertToExpression_WhenLikeUsedOnNonString_ShouldThrow()
    {
        // Arrange
        var converter = new SqlToLinqConverter();
        var clause = "Score like '1%'";

        // Act
        var action = () => converter.ConvertToExpression<TestEntity>(clause);

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
