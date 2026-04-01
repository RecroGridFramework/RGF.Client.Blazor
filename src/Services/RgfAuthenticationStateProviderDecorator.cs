using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

internal static class RgfAuthenticationStateProviderDecorator
{
    public static IServiceCollection DecorateAuthenticationStateProvider(this IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(serviceDescriptor => serviceDescriptor.ServiceType == typeof(AuthenticationStateProvider))
            ?? throw new InvalidOperationException("AuthenticationStateProvider must be registered before decorating it.");

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(RgfInnerAuthenticationStateProviderHolder), serviceProvider =>
        {
            return new RgfInnerAuthenticationStateProviderHolder(CreateInnerProvider(serviceProvider, descriptor));
        }, descriptor.Lifetime));

        services.Add(new ServiceDescriptor(typeof(AuthenticationStateProvider), serviceProvider =>
        {
            return new RgfSessionAwareAuthenticationStateProvider(
                serviceProvider.GetRequiredService<RgfInnerAuthenticationStateProviderHolder>().Provider,
                serviceProvider.GetRequiredService<IRgfAuthenticationSessionMonitor>());
        }, descriptor.Lifetime));

        return services;
    }

    private static AuthenticationStateProvider CreateInnerProvider(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is AuthenticationStateProvider instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (AuthenticationStateProvider)descriptor.ImplementationFactory(serviceProvider);
        }

        return (AuthenticationStateProvider)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!);
    }

    private sealed class RgfInnerAuthenticationStateProviderHolder
    {
        public RgfInnerAuthenticationStateProviderHolder(AuthenticationStateProvider provider)
        {
            Provider = provider;
        }

        public AuthenticationStateProvider Provider { get; }
    }
}
