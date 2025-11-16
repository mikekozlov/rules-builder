using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Prosperity.Api.Infrastructure.RulesEngine;

public sealed class CptRuleIngestionHostedService : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public CptRuleIngestionHostedService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<CptRuleIngestionService>();
        await ingestionService.IngestAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
