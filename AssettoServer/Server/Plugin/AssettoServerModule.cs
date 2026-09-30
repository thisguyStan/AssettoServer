using System;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Server.Plugin;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class AssettoServerModule
{
    public virtual object? ReferenceConfiguration => null;

    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    public virtual void ConfigureDependencies(IServiceCollection services)
    {
    }

    public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
    }
}

public abstract class AssettoServerModule<TConfig> : AssettoServerModule where TConfig : new()
{
    public override object? ReferenceConfiguration => new TConfig();
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSingletonHostedService<TService>(this IServiceCollection services)
        where TService : class, IHostedService
    {
        services.AddSingleton<TService>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<TService>());
        return services;
    }

    public static IServiceCollection AddAutoActivatedSingleton<TService>(this IServiceCollection services)
        where TService : class
    {
        services.AddSingleton<TService>();
        services.AddSingleton(new AutoActivationRegistration(typeof(TService)));
        return services;
    }

    internal static void ActivateAutoActivatedServices(this IServiceProvider serviceProvider)
    {
        foreach (var registration in serviceProvider.GetServices<AutoActivationRegistration>())
        {
            serviceProvider.GetRequiredService(registration.ServiceType);
        }
    }

    private sealed record AutoActivationRegistration(Type ServiceType);
}
