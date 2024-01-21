using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;
using TrafficPlugin.Configuration;

namespace DynamicTrafficPlugin;

public class DynamicTrafficDensity : CriticalBackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly WeatherManager _weatherManager;
    private readonly AiParams _aiParams;
    private readonly DynamicTrafficConfiguration _dynamicTrafficConfiguration;

    public DynamicTrafficDensity(ACServerConfiguration configuration, 
        WeatherManager weatherManager, 
        AiParams aiParams,
        DynamicTrafficConfiguration dynamicTrafficConfiguration,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        _weatherManager = weatherManager;
        _aiParams = aiParams;
        _dynamicTrafficConfiguration = dynamicTrafficConfiguration;
    }

    private float GetDensity(double hourOfDay)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Math.Truncate(hourOfDay) == hourOfDay)
        {
            return _dynamicTrafficConfiguration.HourlyTrafficDensity![(int)hourOfDay];
        }

        int lowerBound = (int)Math.Floor(hourOfDay);
        int higherBound = (int)Math.Ceiling(hourOfDay) % 24;

        return (float)MathUtils.Lerp(_dynamicTrafficConfiguration.HourlyTrafficDensity![lowerBound], _dynamicTrafficConfiguration.HourlyTrafficDensity![higherBound], hourOfDay - lowerBound);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                double hours = _weatherManager.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0 / 3600.0;
                _aiParams.TrafficDensity = GetDensity(hours);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in dynamic traffic density update");
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMinutes(10.0 / _configuration.Server.TimeOfDayMultiplier), stoppingToken);
            }
        }
    }
}
