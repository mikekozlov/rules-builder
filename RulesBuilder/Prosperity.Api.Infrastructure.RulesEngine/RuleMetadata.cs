using System;
using System.Collections.Generic;

namespace Prosperity.Api.Infrastructure.RulesEngine;

public sealed record RuleMetadata(
    RuleContact Author,
    DateTime CreatedAt,
    RuleContact LastUpdatedBy,
    DateTime LastUpdatedAt,
    RuleVersion Version,
    RuleVersion? PreviousVersion,
    IReadOnlyCollection<RuleTag> Tags);

public sealed record RuleContact(string Name, string Email);

public sealed record RuleVersion(int Major, int Minor, int Patch, string ChangeType, string ChangeReason);

public sealed record RuleTag(string Category, string Value);
