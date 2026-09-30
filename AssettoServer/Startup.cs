using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using AssettoServer.Commands;
using AssettoServer.Commands.Contexts;
using AssettoServer.Commands.TypeParsers;
using AssettoServer.Network;
using AssettoServer.Network.Http;
using AssettoServer.Network.Http.Authentication;
using AssettoServer.Network.Rcon;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Udp;
using AssettoServer.Server;
using AssettoServer.Server.Admin;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Blacklist;
using AssettoServer.Server.CMContentProviders;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Configuration.Serialization;
using AssettoServer.Server.GeoParams;
using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Steam;
using AssettoServer.Server.TrackParams;
using AssettoServer.Server.UserGroup;
using AssettoServer.Server.Weather;
using AssettoServer.Server.Whitelist;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Qmmands;

namespace AssettoServer;

public class Startup
{
    private readonly ACServerConfiguration _configuration;
    private readonly ACPluginLoader _loader;

    public Startup(ACServerConfiguration configuration)
    {
        _configuration = configuration;
        _loader = new ACPluginLoader(configuration.LoadPluginsFromWorkdir);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
            // This defaults to false, explicitly adding it here in case someone thinks it could be changed...
            // We can't due to dependencies between hosted services
            options.ServicesStartConcurrently = false;
            // Same for shutdown, otherwise we might not get the server shutdown chat message out
            options.ServicesStopConcurrently = false;
        });
        services.AddCors(options =>
        {
            options.AddPolicy(name: "ServerQueryPolicy",
                policy => { policy.WithOrigins(_configuration.Extra.CorsAllowedOrigins?.ToArray() ?? []); });
        });
        services.AddAuthentication(o => { o.DefaultScheme = ""; })
            .AddScheme<ACClientAuthenticationSchemeOptions, ACClientAuthenticationHandler>(
                ACClientAuthenticationSchemeOptions.Scheme, _ => { });
        services.AddAuthorization();
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, JsonSourceGenerationContext.Default);
        });
        services.AddControllers(options => { options.OutputFormatters.Add(new LuaOutputFormatter()); });

        var mvcBuilder = services.AddControllers();

        if (_configuration.Extra.EnablePlugins != null)
        {
            _loader.LoadPlugins(_configuration.Extra.EnablePlugins);

            foreach (var plugin in _loader.LoadedPlugins)
            {
                plugin.Instance.ConfigureServices(services);
                mvcBuilder.AddApplicationPart(plugin.Assembly);
            }
        }

        services.AddSingleton(_configuration);
        services.AddSingleton(_loader);

        // Registration order == order in which hosted services are started
        services.AddSingletonHostedService<ACServer>();
        services.AddSingletonHostedService<SessionManager>();
        services.AddSingletonHostedService<ACTcpServer>();
        services.AddSingletonHostedService<ACUdpServer>();
        new WeatherModule(_configuration).ConfigureDependencies(services);
        new AiModule(_configuration).ConfigureDependencies(services);
        services.AddSingleton<FileBasedUserGroupProvider>();
        services.AddSingleton<IUserGroupProvider>(provider =>
            provider.GetRequiredService<FileBasedUserGroupProvider>());
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<FileBasedUserGroupProvider>());
        services.AddSingletonHostedService<SignalHandler>();
        services.AddSingletonHostedService<HttpInfoCache>();
        RegisterLegacyPluginInterface();
        RegisterSteam();
        RegisterRcon();

        foreach (var plugin in _loader.LoadedPlugins)
        {
            if (plugin.ConfigurationType != null)
            {
                services.AddTransient(plugin.ConfigurationType);
            }

            plugin.Instance.ConfigureDependencies(services);
        }

        // Do this last so we don't register before a plugin fails to start
        services.AddSingletonHostedService<UpnpService>();
        services.AddSingleton<IHostedService, KunosLobbyRegistration>();

        // No hosted services below this line

        services.AddTransient<HttpClient>();
        services.AddTransient<ACTcpClient>();
        services.AddTransient<EntryCar>();
        services.AddTransient<EntryCar.Factory>(provider =>
        {
            var factory = provider.GetRequiredService<Func<string, string?, byte, EntryCar>>();
            return (model, skin, sessionId) => factory(model, skin, sessionId);
        });
        services.AddTransient<ChatCommandContext>();
        services.AddTransient<RconCommandContext>();
        services.AddTransient<SessionState>();
        services.AddTransient<ACClientTypeParser>();
        services.AddAutoActivatedSingleton<ChatService>();
        services.AddSingleton<CSPFeatureManager>();
        services.AddSingleton<UserGroupManager>();
        services.AddTransient<FileBasedUserGroup>();
        services.AddSingleton<IAdminService, AdminService>();
        services.AddSingleton<IBlacklistService, BlacklistService>();
        services.AddSingleton<IWhitelistService, WhitelistService>();
        services.AddSingleton<ITrackParamsProvider, IniTrackParamsProvider>();
        services.AddSingleton<CSPServerScriptProvider>();
        services.AddSingleton<CSPClientMessageTypeManager>();
        services.AddSingleton<CSPClientMessageHandler>();
        services.AddSingleton<VoteManager>();
        services.AddSingleton<EntryCarManager>();
        services.AddTransient<IGeoParamsProvider, IpApiGeoParamsProvider>();
        services.AddSingleton<GeoParamsManager>();
        services.AddSingleton<ChecksumManager>();
        services.AddSingleton<CSPServerExtraOptions>();
        services.AddSingleton<OpenSlotFilterChain>();
        services.AddTransient<IOpenSlotFilter, WhitelistSlotFilter>();
        services.AddTransient<IOpenSlotFilter, GuidSlotFilter>();
        services.AddTransient<ConfigurationSerializer>();
        services.AddSingleton<ICMContentProvider, DefaultCMContentProvider>();
        services.AddSingleton<CommandService>();

        if (_configuration.GeneratePluginConfigs)
        {
            var loader = new ACPluginLoader(_configuration.LoadPluginsFromWorkdir);
            loader.LoadPlugins(loader.AvailablePlugins.Select(p => p.Key).ToList());
            _configuration.LoadPluginConfiguration(loader, null);
        }

        _configuration.LoadPluginConfiguration(_loader, services);

        void RegisterLegacyPluginInterface()
        {
            if (_configuration.Extra.EnableLegacyPluginInterface)
            {
                services.AddSingletonHostedService<UdpPluginServer>();
            }
        }

        void RegisterSteam()
        {
            if (_configuration.Extra.UseSteamAuth)
            {
                services.AddSingleton<NativeSteam>();
                services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<NativeSteam>());
                services.AddSingleton<ISteam>(provider => provider.GetRequiredService<NativeSteam>());
                services.AddAutoActivatedSingleton<SteamManager>();
                services.AddTransient<IOpenSlotFilter, SteamSlotFilter>();
            }
        }

        void RegisterRcon()
        {
            if (_configuration.Extra.RconPort != 0)
            {
                services.AddTransient<RconClient>();
                services.AddSingletonHostedService<RconServer>();
            }
        }
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = "/static",
            ServeUnknownFileTypes = true,
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapMetrics();
            endpoints.MapControllers();
        });

        foreach (var plugin in _loader.LoadedPlugins)
        {
            var wwwrootPath = Path.Combine(plugin.Directory, "wwwroot");
            if (Directory.Exists(wwwrootPath))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(wwwrootPath),
                    RequestPath = $"/static/{plugin.Name}",
                    ServeUnknownFileTypes = true,
                });
            }

            plugin.Instance.Configure(app, env);
        }
    }
}
