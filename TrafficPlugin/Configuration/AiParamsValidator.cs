using FluentValidation;
using JetBrains.Annotations;

namespace TrafficPlugin.Configuration;

[UsedImplicitly]
public class AiParamsValidator : AbstractValidator<AiParams>
{
    public AiParamsValidator()
    {
        RuleFor(ai => ai.MinSpawnDistancePoints).LessThanOrEqualTo(ai => ai.MaxSpawnDistancePoints);
        RuleFor(ai => ai.MinAiSafetyDistanceMeters).LessThanOrEqualTo(ai => ai.MaxAiSafetyDistanceMeters);
        RuleFor(ai => ai.MinSpawnProtectionTimeSeconds).LessThanOrEqualTo(ai => ai.MaxSpawnProtectionTimeSeconds);
        RuleFor(ai => ai.MinCollisionStopTimeSeconds).LessThanOrEqualTo(ai => ai.MaxCollisionStopTimeSeconds);
        RuleFor(ai => ai.MaxSpeedVariationPercent).InclusiveBetween(0, 1);
        RuleFor(ai => ai.DefaultAcceleration).GreaterThan(0);
        RuleFor(ai => ai.DefaultDeceleration).GreaterThan(0);
        RuleFor(ai => ai.NamePrefix).NotNull();
        RuleFor(ai => ai.IgnoreObstaclesAfterSeconds).GreaterThanOrEqualTo(0);
        RuleFor(ai => ai.CarSpecificOverrides).NotNull();
        RuleFor(ai => ai.AiBehaviorUpdateIntervalHz).GreaterThan(0);
        RuleFor(ai => ai.LaneCountSpecificOverrides).NotNull();
        RuleForEach(ai => ai.LaneCountSpecificOverrides).ChildRules(overrides =>
        {
            overrides.RuleFor(o => o.Key).GreaterThan(0);
            overrides.RuleFor(o => o.Value.MinAiSafetyDistanceMeters).LessThanOrEqualTo(o => o.Value.MaxAiSafetyDistanceMeters);
        });
    }
}
