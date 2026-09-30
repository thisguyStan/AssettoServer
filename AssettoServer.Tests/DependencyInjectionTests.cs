using AssettoServer.Server.Plugin;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Tests;

public class DependencyInjectionTests
{
    [Test]
    public void DryIocServiceProvider_PreservesFactoriesPluginInjectionAndHostedSingletons()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ActivationTracker>();
        services.AddAutoActivatedSingleton<AutoActivationService>();
        services.AddSingletonHostedService<TestHostedService>();
        services.AddTransient<FactoryTarget>();
        services.AddTransient<LazyTarget>();
        services.AddTransient<CarFactory>(provider =>
        {
            var factory = provider.GetRequiredService<Func<string, string?, byte, FactoryTarget>>();
            return (model, skin, sessionId) => factory(model, skin, sessionId);
        });
        services.AddTransient<FactoryConsumer>();
        services.AddSingleton<PluginDependency>();
        new TestPluginModule().ConfigureDependencies(services);

        var factory = new DryIocServiceProviderFactory();
        using var providerBuilder = factory.CreateBuilder(services);
        var provider = factory.CreateServiceProvider(providerBuilder);

        var consumer = provider.GetRequiredService<FactoryConsumer>();
        var car = consumer.Create("runtime model", null, 7);

        var hostedService = provider.GetRequiredService<IHostedService>();

        var activationTracker = provider.GetRequiredService<ActivationTracker>();
        var activationCountBefore = activationTracker.ActivationCount;
        provider.ActivateAutoActivatedServices();
        var activationCountAfter = activationTracker.ActivationCount;

        var pluginService = provider.GetRequiredService<PluginService>();

        Assert.Multiple(() =>
        {
            Assert.That(car.Model, Is.EqualTo("runtime model"));
            Assert.That(car.Skin, Is.Null);
            Assert.That(car.SessionId, Is.EqualTo(7));
            Assert.That(consumer.Lazy.Value, Is.Not.Null);
            Assert.That(hostedService, Is.SameAs(provider.GetRequiredService<TestHostedService>()));
            Assert.That(activationCountBefore, Is.Zero);
            Assert.That(activationCountAfter, Is.EqualTo(1));
            Assert.That(pluginService.Dependency, Is.SameAs(provider.GetRequiredService<PluginDependency>()));
        });
    }

    public sealed class FactoryConsumer(CarFactory factory, Lazy<LazyTarget> lazy)
    {
        public Lazy<LazyTarget> Lazy { get; } = lazy;

        public FactoryTarget Create(string model, string? skin, byte sessionId) => factory(model, skin, sessionId);
    }

    public delegate FactoryTarget CarFactory(string model, string? skin, byte sessionId);

    public sealed class FactoryTarget
    {
        public string Model { get; }
        public string? Skin { get; }
        public byte SessionId { get; }

        public FactoryTarget(string model, string? skin, byte sessionId)
        {
            Model = model;
            Skin = skin;
            SessionId = sessionId;
        }
    }

    public sealed class LazyTarget
    {
    }

    public sealed class TestHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public sealed class ActivationTracker
    {
        public int ActivationCount { get; set; }
    }

    public sealed class AutoActivationService
    {
        public AutoActivationService(ActivationTracker tracker)
        {
            tracker.ActivationCount++;
        }
    }

    public sealed class PluginDependency
    {
    }

    public sealed class PluginService
    {
        public PluginDependency Dependency { get; }

        public PluginService(PluginDependency dependency)
        {
            Dependency = dependency;
        }
    }

    private sealed class TestPluginModule : AssettoServerModule
    {
        public override void ConfigureDependencies(IServiceCollection services)
        {
            services.AddTransient<PluginService>();
        }
    }
}
