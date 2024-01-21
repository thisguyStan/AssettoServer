using AssettoServer.Server;
using AssettoServer.Server.Configuration;

namespace TrafficPlugin.Configuration;

public class AiParamsFixer
{
    private readonly AiParams _aiParams;
    private readonly ACServerConfiguration _configuration;

    public AiParamsFixer(AiParams aiParams, ACServerConfiguration configuration)
    {
        _aiParams = aiParams;
        _configuration = configuration;

        ApplyConfigurationFixes();
    }
    
    
    private void ApplyConfigurationFixes()
    {
        if (_aiParams.AutoAssignTrafficCars)
            // if (Extra is { EnableAi: true, AiParams.AutoAssignTrafficCars: true })
        {
            foreach (var entry in _configuration.EntryList.Cars)
            {
                if (entry.Model.Contains("traffic"))
                {
                    entry.AiMode = AiMode.Fixed;
                }
            }
        }

        if (_aiParams.AiPerPlayerTargetCount == 0)
        {
            _aiParams.AiPerPlayerTargetCount = _configuration.EntryList.Cars.Count(c => c.AiMode != AiMode.None);
        }

        if (_aiParams.MaxAiTargetCount == 0)
        {
            _aiParams.MaxAiTargetCount = _configuration.EntryList.Cars.Count(c => c.AiMode == AiMode.None) * _aiParams.AiPerPlayerTargetCount;
        }
    }
}
