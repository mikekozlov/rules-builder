using Prosperity.Api.Infrastructure.RulesEngine.Models;

namespace Prosperity.Api.Infrastructure.RulesEngine.Abstractions;

public interface IRuleStore
{
    Task SaveAsync(string ruleSetKey, StoredRule rule, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StoredRule>> GetAllAsync(string ruleSetKey, CancellationToken cancellationToken = default);

    Task<StoredRule?> GetAsync(string ruleSetKey, string ruleName, CancellationToken cancellationToken = default);
}
