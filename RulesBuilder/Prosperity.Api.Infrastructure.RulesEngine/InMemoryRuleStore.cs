namespace Prosperity.Api.Infrastructure.RulesEngine;

public sealed class InMemoryRuleStore : IRuleStore
{
    private readonly Dictionary<string, Dictionary<string, StoredRule>> _store;
    private readonly object _sync;

    public InMemoryRuleStore()
    {
        _store = new Dictionary<string, Dictionary<string, StoredRule>>();
        _sync = new object();
    }

    public Task SaveAsync(string ruleSetKey, StoredRule rule, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_store.TryGetValue(ruleSetKey, out var rulesForSet))
            {
                rulesForSet = new Dictionary<string, StoredRule>();
                _store[ruleSetKey] = rulesForSet;
            }

            rulesForSet[rule.Name] = rule;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<StoredRule>> GetAllAsync(string ruleSetKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_store.TryGetValue(ruleSetKey, out var rulesForSet))
            {
                var result = rulesForSet.Values.ToArray();
                return Task.FromResult<IReadOnlyCollection<StoredRule>>(result);
            }

            return Task.FromResult<IReadOnlyCollection<StoredRule>>(Array.Empty<StoredRule>());
        }
    }

    public Task<StoredRule?> GetAsync(string ruleSetKey, string ruleName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_store.TryGetValue(ruleSetKey, out var rulesForSet) &&
                rulesForSet.TryGetValue(ruleName, out var rule))
            {
                return Task.FromResult<StoredRule?>(rule);
            }

            return Task.FromResult<StoredRule?>(null);
        }
    }
}
