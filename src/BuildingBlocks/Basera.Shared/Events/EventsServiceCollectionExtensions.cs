using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Events;

public static class EventsServiceCollectionExtensions
{
    /// <summary>Registers every concrete <see cref="IDomainEventHandler{TEvent}"/> in the assembly. Modules call this from <c>AddModule</c>.</summary>
    public static IServiceCollection AddDomainEventHandlers(this IServiceCollection services, Assembly assembly)
    {
        var registrations = assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                .Select(contract => (contract, type)));

        foreach (var (contract, implementation) in registrations)
        {
            services.AddScoped(contract, implementation);
        }

        return services;
    }
}
