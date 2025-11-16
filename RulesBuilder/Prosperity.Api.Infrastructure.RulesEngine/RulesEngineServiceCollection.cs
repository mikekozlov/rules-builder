using Microsoft.Extensions.DependencyInjection;

namespace Prosperity.Api.Infrastructure.RulesEngine;

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
