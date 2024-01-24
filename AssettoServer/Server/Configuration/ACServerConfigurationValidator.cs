﻿using FluentValidation;

namespace AssettoServer.Server.Configuration;

public class ACServerConfigurationValidator : AbstractValidator<ACServerConfiguration>
{
    public ACServerConfigurationValidator()
    {
        RuleFor(cfg => cfg.Extra).ChildRules(extra =>
        {
            extra.RuleFor(x => x.ValidateDlcOwnership).NotNull();
            extra.RuleFor(x => x.ServerDescription).NotNull();
            extra.RuleFor(x => x.RainTrackGripReductionPercent).InclusiveBetween(0, 0.5);
            extra.RuleFor(x => x.IgnoreConfigurationErrors).NotNull();
            extra.RuleFor(x => x.UserGroups).NotNull();
            extra.RuleFor(x => x.BlacklistUserGroup).NotEmpty();
            extra.RuleFor(x => x.WhitelistUserGroup).NotEmpty();
            extra.RuleFor(x => x.AdminUserGroup).NotEmpty();

            
        });

        RuleFor(cfg => cfg.Server).ChildRules(server =>
        {
            server.RuleFor(s => s.Track).NotEmpty();
            server.RuleFor(s => s.TrackConfig).NotNull();
            server.RuleFor(s => s.FuelConsumptionRate).GreaterThanOrEqualTo(0);
            server.RuleFor(s => s.MechanicalDamageRate).GreaterThanOrEqualTo(0);
            server.RuleFor(s => s.TyreConsumptionRate).GreaterThanOrEqualTo(0);
            server.RuleFor(s => s.LegalTyres).NotNull();
            server.RuleFor(s => s.WelcomeMessagePath).NotNull();
            server.RuleFor(s => s.TimeOfDayMultiplier).GreaterThanOrEqualTo(0);

            server.RuleForEach(s => s.Weathers).ChildRules(weather =>
            {
                weather.RuleFor(w => w.BaseTemperatureAmbient).GreaterThanOrEqualTo(0);
                weather.RuleFor(w => w.WindBaseSpeedMin).LessThanOrEqualTo(w => w.WindBaseSpeedMax);
            });

            server.RuleFor(s => s.DynamicTrack).NotNull().ChildRules(dynTrack =>
            {
                dynTrack.RuleFor(d => d.BaseGrip).InclusiveBetween(0, 1);
            });
        });

        RuleFor(cfg => cfg.EntryList).ChildRules(entryList =>
        {
            entryList.RuleForEach(el => el.Cars).ChildRules(car =>
            {
                car.RuleFor(c => c.Model).NotNull();
                car.RuleFor(c => c.Guid).NotNull();
                car.RuleFor(c => c.Restrictor).InclusiveBetween(0, 400);
                car.RuleFor(c => c.Ballast).GreaterThanOrEqualTo(0);
            });
        });
    }
}
