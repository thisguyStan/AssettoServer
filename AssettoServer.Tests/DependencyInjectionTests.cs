using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Diagnostics;
using AssettoServer.Commands;
using AssettoServer.Commands.Contexts;
using AssettoServer.Network.Rcon;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Udp;
using AssettoServer.Server;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Configuration.Kunos;
using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Steam;
using AssettoServer.Server.UserGroup;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Model;
using McMaster.NETCore.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Tests;

[NonParallelizable]
public class DependencyInjectionTests
{
    private string _previousDirectory = null!;
    private string _directory = null!;
    private string _pluginsDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _previousDirectory = Directory.GetCurrentDirectory();
        _directory = Path.Combine(Path.GetTempPath(), "AssettoServerDependencyInjectionTests",
            Guid.NewGuid().ToString("N"));
        _pluginsDirectory = Path.Combine(_directory, "plugins");
        Directory.CreateDirectory(_pluginsDirectory);
        Directory.SetCurrentDirectory(_directory);
        Directory.CreateDirectory("cfg");
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_previousDirectory);
        Directory.Delete(_directory, true);
    }

    [Test]
    public void CoreServicesPreserveSingletonAliasesAndHostedServiceOrder()
    {
        var configuration = CreateConfiguration();
        using var provider = CreateServices(configuration).BuildServiceProvider(ValidatedOptions());
        provider.ActivateSingletons();

        var hostedServices = provider.GetServices<IHostedService>()
            .Where(service => service.GetType().Assembly == typeof(ACServer).Assembly).ToArray();
        Assert.That(hostedServices.Select(service => service.GetType()), Is.EqualTo(new[]
        {
            typeof(ACServer), typeof(SessionManager), typeof(ACTcpServer), typeof(ACUdpServer),
            typeof(WeatherManager), typeof(FileBasedUserGroupProvider), typeof(SignalHandler),
            typeof(AssettoServer.Network.Http.HttpInfoCache), typeof(UpnpService), typeof(KunosLobbyRegistration)
        }));
        Assert.That(hostedServices[0], Is.SameAs(provider.GetRequiredService<ACServer>()));
        Assert.That(hostedServices[1], Is.SameAs(provider.GetRequiredService<SessionManager>()));
        Assert.That(hostedServices[4], Is.SameAs(provider.GetRequiredService<WeatherManager>()));
        Assert.That(provider.GetRequiredService<IUserGroupProvider>(),
            Is.SameAs(provider.GetRequiredService<FileBasedUserGroupProvider>()));
        var chatService = provider.GetRequiredService<ChatService>();
        Assert.That(provider.GetRequiredService<ChatService>(), Is.SameAs(chatService));
        Assert.That(provider.GetRequiredService<OpenSlotFilterChain>(), Is.Not.Null);

        var sessionConfiguration = new SessionConfiguration();
        var session = provider.GetRequiredService<Func<SessionConfiguration, SessionState>>()(sessionConfiguration);
        Assert.That(session.Configuration, Is.SameAs(sessionConfiguration));
    }

    [Test]
    public void AspNetHostBuildsWithTheDefaultServiceProvider()
    {
        var configuration = CreateConfiguration();
        var loader = new ACPluginLoader(false, _pluginsDirectory);
        using var host = Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureWebHostDefaults(web => web.UseStartup(_ => new Startup(configuration, loader)))
            .Build();
        Assert.That(host.Services, Is.InstanceOf<ServiceProvider>());
        host.Services.ActivateSingletons();
        Assert.That(host.Services.GetRequiredService<EntryCar.Factory>()("host_car", null, 1).Model,
            Is.EqualTo("host_car"));
    }

    [Test]
    public void BundledPluginsResolveTheirPerCarAndSessionFactories()
    {
        var services = CreateServices(CreateConfiguration());
        services.AddSingleton(new AutoModerationPlugin.AutoModerationConfiguration());
        new AutoModerationPlugin.AutoModerationModule().ConfigureServices(services);
        services.AddSingleton(new TagModePlugin.TagModeConfiguration());
        new TagModePlugin.TagModeModule().ConfigureServices(services);
        using var provider = services.BuildServiceProvider(ValidatedOptions());
        var car = provider.GetRequiredService<EntryCar.Factory>()("plugin_car", "skin", 1);
        var moderationFactory = provider.GetRequiredService<Func<EntryCar, AutoModerationPlugin.EntryCarAutoModeration>>();
        var moderation = moderationFactory(car);
        Assert.That(moderationFactory(car), Is.Not.SameAs(moderation));
        var tagFactory = provider.GetRequiredService<Func<EntryCar, TagModePlugin.EntryCarTagMode>>();
        var tag = tagFactory(car);
        Assert.That(tagFactory(car), Is.Not.SameAs(tag));
        var session = provider.GetRequiredService<TagModePlugin.TagSession.Factory>()(car);
        Assert.That(session.InitialTagger, Is.SameAs(car));
        Assert.That(provider.GetServices<IHostedService>().Single(service => service is AutoModerationPlugin.AutoModerationPlugin),
            Is.SameAs(provider.GetRequiredService<AutoModerationPlugin.AutoModerationPlugin>()));
        Assert.That(provider.GetServices<IHostedService>().Single(service => service is TagModePlugin.TagModePlugin),
            Is.SameAs(provider.GetRequiredService<TagModePlugin.TagModePlugin>()));
    }

    [Test]
    public async Task EntryCarsAndTcpClientsReceiveRuntimeArgumentsAndSharedServices()
    {
        using var provider = CreateServices(CreateConfiguration()).BuildServiceProvider(ValidatedOptions());
        var factory = provider.GetRequiredService<EntryCar.Factory>();
        var first = factory("first_model", "first_skin", 7);
        var second = factory("second_model", null, 8);
        Assert.That(first, Is.InstanceOf<IEntryCar<ACTcpClient>>());
        Assert.That(first.Model, Is.EqualTo("first_model"));
        Assert.That(first.Skin, Is.EqualTo("first_skin"));
        Assert.That(first.SessionId, Is.EqualTo(7));
        Assert.That(second.Model, Is.EqualTo("second_model"));
        Assert.That(second.Skin, Is.Empty);
        Assert.That(second, Is.Not.SameAs(first));

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var remote = new TcpClient();
        var connect = remote.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
        using var accepted = await listener.AcceptTcpClientAsync();
        await connect;
        var client = provider.GetRequiredService<Func<TcpClient, ACTcpClient>>()(accepted);
        Assert.That(client.TcpClient, Is.SameAs(accepted));
        client.EntryCar = first;
        first.Client = client;
        Assert.That(((IEntryCar<ACTcpClient>)first).Client, Is.SameAs(client));
        var context = provider.GetRequiredService<Func<ACTcpClient, ChatCommandContext>>()(client);
        Assert.That(context.Client, Is.SameAs(client));
        Assert.That(context.Services.GetRequiredService<EntryCarManager>(),
            Is.SameAs(provider.GetRequiredService<EntryCarManager>()));
    }

    [Test]
    public void AiFactoriesInjectTheExistingCarAndSharedSpline()
    {
        var configuration = CreateConfiguration("EnableAi: true\n");
        var splinePath = Path.Combine(_directory, "test-spline.aip");
        var points = Enumerable.Range(0, 3).Select(id => new SplinePoint
        {
            Id = id,
            Position = new Vector3(id * 20, 0, 0),
            PreviousId = (id + 2) % 3,
            NextId = (id + 1) % 3,
            LeftId = -1,
            RightId = -1,
            LanesId = -1,
            JunctionStartId = -1,
            JunctionEndId = -1
        }).ToArray();
        var spline = new MutableAiSpline(new Dictionary<string, FastLane> { ["test"] = new() { Points = points } },
            3.5f);
        new AiSplineWriter().ToFile(spline, splinePath);
        var services = CreateServices(configuration);
        services.RemoveAll<AiSpline>();
        services.AddSingleton(_ => new AiSpline(splinePath));
        using var provider = services.BuildServiceProvider(ValidatedOptions());
        provider.ActivateSingletons();
        var car = provider.GetRequiredService<EntryCar.Factory>()("ai_model", "skin", 2);
        var stateFactory = provider.GetRequiredService<Func<EntryCar, AiState>>();
        var state = stateFactory(car);
        Assert.That(state.EntryCar, Is.SameAs(car));
        Assert.That(stateFactory(car), Is.Not.SameAs(state));
        Assert.That(provider.GetServices<IHostedService>().Single(service => service is AiBehavior),
            Is.SameAs(provider.GetRequiredService<AiBehavior>()));
        Assert.That(provider.GetRequiredService<AiUpdater>(), Is.Not.Null);
    }

    [Test]
    public void OptionalSteamAndRconServicesPreserveAliasesAndOrder()
    {
        var configuration = CreateConfiguration(
            "UseSteamAuth: true\nRconPort: 9601\nEnableLegacyPluginInterface: true\n",
            "UDP_PLUGIN_ADDRESS=127.0.0.1:11000\nUDP_PLUGIN_LOCAL_PORT=11001\n");
        using var provider = CreateServices(configuration).BuildServiceProvider(ValidatedOptions());
        provider.ActivateSingletons();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        Assert.That(hostedServices.Single(service => service is NativeSteam),
            Is.SameAs(provider.GetRequiredService<ISteam>()));
        Assert.That(hostedServices.Single(service => service is RconServer),
            Is.SameAs(provider.GetRequiredService<RconServer>()));
        Assert.That(Array.FindIndex(hostedServices, service => service is UdpPluginServer),
            Is.LessThan(Array.FindIndex(hostedServices, service => service is NativeSteam)));
        Assert.That(Array.FindIndex(hostedServices, service => service is NativeSteam),
            Is.LessThan(Array.FindIndex(hostedServices, service => service is RconServer)));
        Assert.That(provider.GetRequiredService<Func<TcpClient, RconClient>>(), Is.Not.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DllPluginsReceiveConfigurationAndPerCarDependencies(bool existingConfiguration)
    {
        if (existingConfiguration)
        {
            File.WriteAllText(Path.Combine("cfg", "plugin_dependency_injection_test_cfg.yml"), "Value: 42\n");
        }

        var configuration = CreateConfiguration("EnablePlugins:\n  - DependencyInjectionTestPlugin\n");
        var loader = new ACPluginLoader(false, _pluginsDirectory);
        using var dllLoader = PluginLoader.CreateFromAssemblyFile(typeof(DependencyInjectionTests).Assembly.Location,
            options => options.PreferSharedTypes = true);
        loader.AvailablePlugins.Add("DependencyInjectionTestPlugin",
            new AvailablePlugin(new PluginConfiguration(), dllLoader, _pluginsDirectory));
        var services = CreateServices(configuration, loader);
        using var provider = services.BuildServiceProvider(ValidatedOptions());
        Assert.That(loader.LoadedPlugins, Has.Count.EqualTo(1));
        var assembly = loader.LoadedPlugins[0].Assembly;
        var pluginType = assembly.GetType(typeof(DependencyInjectionTestPlugin).FullName!, true)!;
        var plugin = provider.GetRequiredService(pluginType);
        var pluginConfiguration = pluginType.GetProperty("Configuration")!.GetValue(plugin)!;
        var carType = assembly.GetType(typeof(DependencyInjectionTestCar).FullName!, true)!;
        var car = provider.GetRequiredService<EntryCar.Factory>()("plugin_car", "skin", 9);
        var factoryType = typeof(Func<,>).MakeGenericType(typeof(EntryCar), carType);
        var factory = (Delegate)provider.GetRequiredService(factoryType);
        var extra = factory.DynamicInvoke(car)!;
        Assert.That(carType.GetProperty("Car")!.GetValue(extra), Is.SameAs(car));
        var carConfiguration = carType.GetProperty("Configuration")!.GetValue(extra)!;
        Assert.That(carConfiguration,
            existingConfiguration ? Is.SameAs(pluginConfiguration) : Is.Not.SameAs(pluginConfiguration));
        Assert.That(carConfiguration.GetType().GetProperty("Value")!.GetValue(carConfiguration),
            Is.EqualTo(existingConfiguration ? 42 : 10));
        Assert.That(carType.GetProperty("Manager")!.GetValue(extra),
            Is.SameAs(provider.GetRequiredService<EntryCarManager>()));
        Assert.That(pluginConfiguration.GetType().GetProperty("Value")!.GetValue(pluginConfiguration),
            Is.EqualTo(existingConfiguration ? 42 : 10));
        Assert.That(provider.GetServices<IHostedService>().Single(service => service.GetType() == pluginType),
            Is.SameAs(plugin));
        var probeType = assembly.GetType(typeof(ActivationProbe).FullName!, true)!;
        var probe = provider.GetRequiredService(probeType);
        Assert.That(probeType.GetProperty("Activated")!.GetValue(probe), Is.False);
        provider.ActivateSingletons();
        Assert.That(probeType.GetProperty("Activated")!.GetValue(probe), Is.True);
        Assert.That(File.Exists(Path.Combine("cfg", "plugin_dependency_injection_test_cfg.yml")), Is.True);
    }

    [Test]
    public void NamedFactoryMatchesRepeatedTypesByNameAndHonorsDefaults()
    {
        var services = new ServiceCollection();
        services.AddSingleton<FactoryDependency>();
        services.AddTransientFactory<NamedFactoryProduct.Factory>();
        using var provider = services.BuildServiceProvider(ValidatedOptions());
        var factory = provider.GetRequiredService<NamedFactoryProduct.Factory>();
        var product = factory("left", "right");
        Assert.That(product.First, Is.EqualTo("left"));
        Assert.That(product.Second, Is.EqualTo("right"));
        Assert.That(product.Enabled, Is.True);
        Assert.That(product.Dependency, Is.SameAs(provider.GetRequiredService<FactoryDependency>()));
        Assert.That(factory("again", "another"), Is.Not.SameAs(product));
    }

    [Test]
    public void FactoryProductsAndTheirInjectedDependenciesAreOwnedByTheResolvingScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<FactoryDependency>();
        services.AddTransientFactory<Func<int, DisposableFactoryProduct>>();
        using var provider = services.BuildServiceProvider(ValidatedOptions());
        DisposableFactoryProduct product;
        using (var scope = provider.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<Func<int, DisposableFactoryProduct>>();
            product = factory(12);
            Assert.That(product.Id, Is.EqualTo(12));
            Assert.That(product.Dependency, Is.SameAs(scope.ServiceProvider.GetRequiredService<FactoryDependency>()));
            Assert.That(product.DisposeCount, Is.Zero);
        }

        Assert.That(product.DisposeCount, Is.EqualTo(1));
    }

    [Test]
    public async Task AsyncFactoryProductsAreDisposedAsynchronously()
    {
        var services = new ServiceCollection();
        services.AddTransientFactory<Func<int, AsyncFactoryProduct>>();
        await using var provider = services.BuildServiceProvider(ValidatedOptions());
        AsyncFactoryProduct product;
        await using (var scope = provider.CreateAsyncScope())
        {
            product = scope.ServiceProvider.GetRequiredService<Func<int, AsyncFactoryProduct>>()(3);
        }

        Assert.That(product.Disposed, Is.True);
    }

    [Test]
    public void FactoryResolutionDoesNotEagerlyResolveItsDependencies()
    {
        var services = new ServiceCollection();
        services.AddTransientFactory<Func<int, DisposableFactoryProduct>>();
        using var provider = services.BuildServiceProvider(ValidatedOptions());
        var factory = provider.GetRequiredService<Func<int, DisposableFactoryProduct>>();
        Assert.That(() => factory(1), Throws.InvalidOperationException);
    }

    [Test]
    public void FactoryProductsAreDisposedBeforeChildrenCreatedInTheirConstructors()
    {
        var services = new ServiceCollection();
        services.AddScoped<FactoryDependency>();
        services.AddTransientFactory<Func<int, DisposableFactoryProduct>>();
        services.AddTransientFactory<Func<int, ParentFactoryProduct>>();
        using var provider = services.BuildServiceProvider(ValidatedOptions());
        ParentFactoryProduct parent;
        using (var scope = provider.CreateScope())
        {
            parent = scope.ServiceProvider.GetRequiredService<Func<int, ParentFactoryProduct>>()(5);
        }

        Assert.That(parent.Disposed, Is.True);
        Assert.That(parent.Child.DisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void FactoryOptionalStructParametersUseDefaultValues()
    {
        var services = new ServiceCollection();
        services.AddTransientFactory<Func<string, OptionalStructFactoryProduct>>();
        using var provider = services.BuildServiceProvider(ValidatedOptions());
        var product = provider.GetRequiredService<Func<string, OptionalStructFactoryProduct>>()("test");
        Assert.That(product.Timestamp, Is.EqualTo(default(DateTime)));
        Assert.That(product.Token, Is.EqualTo(default(CancellationToken)));
    }

    [Test]
    public void DisposedScopesRejectFactoriesAndDisposeAnyConstructedProduct()
    {
        var services = new ServiceCollection();
        services.AddTransientFactory<Func<int, AsyncFactoryProduct>>();
        using var provider = services.BuildServiceProvider(ValidatedOptions());
        var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<Func<int, AsyncFactoryProduct>>();
        scope.Dispose();
        Assert.That(() => factory(1), Throws.InstanceOf<ObjectDisposedException>());
    }

    private ACServerConfiguration CreateConfiguration(string extra = "", string serverSettings = "")
    {
        if (serverSettings.Length > 0)
        {
            using var stream =
                typeof(Startup).Assembly.GetManifestResourceStream("AssettoServer.Assets.server_cfg.ini")!;
            using var reader = new StreamReader(stream);
            File.WriteAllText(Path.Combine("cfg", "server_cfg.ini"),
                reader.ReadToEnd().Replace("[PRACTICE]", serverSettings + "\n[PRACTICE]"));
        }

        File.WriteAllText(Path.Combine("cfg", "extra_cfg.yml"),
            extra + (extra.Contains("UseSteamAuth:") ? "" : "UseSteamAuth: false\n"));
        return new ACServerConfiguration(null, ConfigurationLocations.FromOptions(null, null, null), false, false,
            null);
    }

    private IServiceCollection CreateServices(ACServerConfiguration configuration, ACPluginLoader? loader = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, TestApplicationLifetime>();
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment
        {
            ContentRootPath = _directory,
            WebRootPath = _directory
        });
        services.AddSingleton<IHostEnvironment>(provider => provider.GetRequiredService<IWebHostEnvironment>());
        services.AddSingleton(_ => new DiagnosticListener("Microsoft.AspNetCore"));
        services.AddSingleton<DiagnosticSource>(provider => provider.GetRequiredService<DiagnosticListener>());
        new Startup(configuration, loader ?? new ACPluginLoader(false, _pluginsDirectory)).ConfigureServices(services);
        return services;
    }

    private static ServiceProviderOptions ValidatedOptions() => new() { ValidateScopes = true, ValidateOnBuild = true };

    public sealed class TestApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }

    public sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = typeof(Startup).Assembly.GetName().Name!;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    public sealed class FactoryDependency : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    public sealed class NamedFactoryProduct(
        string second,
        FactoryDependency dependency,
        string first,
        bool enabled = true)
    {
        public delegate NamedFactoryProduct Factory(string first, string second);

        public string First { get; } = first;
        public string Second { get; } = second;
        public FactoryDependency Dependency { get; } = dependency;
        public bool Enabled { get; } = enabled;
    }

    public sealed class DisposableFactoryProduct(int id, FactoryDependency dependency) : IDisposable
    {
        public int Id { get; } = id;
        public FactoryDependency Dependency { get; } = dependency;
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            Assert.That(Dependency.Disposed, Is.False, "Products must be disposed before their dependencies.");
            DisposeCount++;
        }
    }

    public sealed class AsyncFactoryProduct(int id) : IAsyncDisposable
    {
        public int Id { get; } = id;
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class ParentFactoryProduct : IDisposable
    {
        public DisposableFactoryProduct Child { get; }
        public bool Disposed { get; private set; }

        public ParentFactoryProduct(int id, Func<int, DisposableFactoryProduct> childFactory) =>
            Child = childFactory(id);

        public void Dispose()
        {
            Assert.That(Child.DisposeCount, Is.Zero,
                "Parents must be disposed before children created in their constructors.");
            Disposed = true;
        }
    }

    public sealed class OptionalStructFactoryProduct(
        string name,
        DateTime timestamp = default,
        CancellationToken token = default)
    {
        public string Name { get; } = name;
        public DateTime Timestamp { get; } = timestamp;
        public CancellationToken Token { get; } = token;
    }
}

public sealed class DependencyInjectionTestModule : AssettoServerModule<DependencyInjectionTestConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<DependencyInjectionTestPlugin>();
        services.AddTransientFactory<Func<EntryCar, DependencyInjectionTestCar>>();
        services.AddSingleton<ActivationProbe>();
        services.AddAutoActivatedSingleton<DependencyInjectionActivatedService>();
    }
}

public sealed class DependencyInjectionTestConfiguration
{
    public int Value { get; init; } = 10;
}

public sealed class DependencyInjectionTestPlugin(DependencyInjectionTestConfiguration configuration) : IHostedService
{
    public DependencyInjectionTestConfiguration Configuration { get; } = configuration;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class DependencyInjectionTestCar(
    EntryCar car,
    DependencyInjectionTestConfiguration configuration,
    EntryCarManager manager)
{
    public EntryCar Car { get; } = car;
    public DependencyInjectionTestConfiguration Configuration { get; } = configuration;
    public EntryCarManager Manager { get; } = manager;
}

public sealed class ActivationProbe
{
    public bool Activated { get; set; }
}

public sealed class DependencyInjectionActivatedService
{
    public DependencyInjectionActivatedService(ActivationProbe probe) => probe.Activated = true;
}
