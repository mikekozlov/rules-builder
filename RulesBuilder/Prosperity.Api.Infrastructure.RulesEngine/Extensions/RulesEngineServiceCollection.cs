using Microsoft.Extensions.DependencyInjection;
using Prosperity.Api.Infrastructure.RulesEngine.Abstractions;
using Prosperity.Api.Infrastructure.RulesEngine.Builders;
using Prosperity.Api.Infrastructure.RulesEngine.Engine;
using Prosperity.Api.Infrastructure.RulesEngine.Hosting;
using Prosperity.Api.Infrastructure.RulesEngine.Ingestion;
using Prosperity.Api.Infrastructure.RulesEngine.Storage;

namespace Prosperity.Api.Infrastructure.RulesEngine.Extensions;

public static class RulesEngineServiceCollection
{
    public static IServiceCollection AddRulesEngine(this IServiceCollection services)
    {
        services.AddSingleton<ISqlToLinqConverter, SqlToLinqConverter>();
        services.AddSingleton<IDynamicRuleBuilder, DynamicRuleBuilder>();
        services.AddSingleton<IRuleStore, InMemoryRuleStore>();
        services.AddScoped(typeof(IDynamicRulesEngine<,>), typeof(DynamicRulesEngine<,>));
        services.AddScoped<CptRuleIngestionService>();
        services.AddHostedService<CptRuleIngestionHostedService>();
        return services;
    }
}
