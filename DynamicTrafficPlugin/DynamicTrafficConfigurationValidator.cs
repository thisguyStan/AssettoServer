using FluentValidation;
using JetBrains.Annotations;

namespace DynamicTrafficPlugin;

[UsedImplicitly]
public class DynamicTrafficConfigurationValidator : AbstractValidator<DynamicTrafficConfiguration>
{
    public DynamicTrafficConfigurationValidator()
    {
        RuleFor(ai => ai.HourlyTrafficDensity)
            .Must(htd => htd?.Count == 24)
            .When(ai => ai.HourlyTrafficDensity != null)
            .WithMessage("HourlyTrafficDensity must have exactly 24 entries");
    }
}
