using AssettoServer.Server.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace DynamicTrafficPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public partial class DynamicTrafficConfiguration : ObservableObject, IValidateConfiguration<DynamicTrafficConfigurationValidator>
{
    [YamlMember(Description = "Dynamic (hourly) traffic density. List must have exactly 24 entries in the format [0.2, 0.5, 1, 0.7, ...]")]
    public List<float>? HourlyTrafficDensity { get; set; }
}
