using Basera.Shared.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Basera.Shared;

public static class SharedServiceCollectionExtensions
{
    /// <summary>Registers the building blocks every module may use.</summary>
    public static IServiceCollection AddBaseraShared(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }
}
