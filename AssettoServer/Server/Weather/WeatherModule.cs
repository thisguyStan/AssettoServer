using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather.Implementation;
using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace AssettoServer.Server.Weather;

public class WeatherModule
{
    private readonly ACServerConfiguration _configuration;

    public WeatherModule(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureDependencies(IServiceCollection services)
    {
        if (_configuration.Extra.EnableWeatherFx)
        {
            services.AddSingleton<IWeatherImplementation, WeatherFxV1Implementation>();
        }
        else
        {
            services.AddSingleton<IWeatherImplementation, VanillaWeatherImplementation>();
        }

        services.AddTransient<RainHelper>();
        services.AddSingleton<IWeatherTypeProvider, DefaultWeatherTypeProvider>();
        services.AddSingletonHostedService<WeatherManager>();
    }
}
