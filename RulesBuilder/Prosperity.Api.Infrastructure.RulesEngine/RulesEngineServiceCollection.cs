using Microsoft.Extensions.DependencyInjection;

namespace Prosperity.Api.Infrastructure.RulesEngine;

public static class RulesEngineServiceCollection
{
    public static IServiceCollection AddRulesEngine(this IServiceCollection services)
    {
        services.AddSingleton<SqlToLinqConverter>();
        services.AddSingleton<DynamicRuleBuilder>();
        services.AddSingleton<IRuleStore, InMemoryRuleStore>();
        services.AddScoped(typeof(IDynamicRulesEngine<,>), typeof(DynamicRulesEngine<,>));
        return services;
    }
}
