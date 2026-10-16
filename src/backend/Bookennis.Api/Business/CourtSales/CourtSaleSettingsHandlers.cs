using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.CourtSales;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtSales;

/// <summary>How the club sells court time (treasurers and admins). Defaults when it never saved settings.</summary>
public record GetCourtSaleSettings(int ClubId) : IQuery<CourtSaleSettingsDto>
{
    public class Handler(AppDbContext context, IPaymentAvailability payments) : IRequestHandler<GetCourtSaleSettings, CourtSaleSettingsDto>
    {
        public async Task<CourtSaleSettingsDto> Handle(GetCourtSaleSettings request, CancellationToken cancellationToken)
        {
            var settings = await context.GetCourtSaleSettings(request.ClubId, cancellationToken);
            return ToDto(settings, await payments.CanAcceptPayments(request.ClubId, cancellationToken));
        }

        internal static CourtSaleSettingsDto ToDto(CourtSaleSettings settings, bool isOnlinePaymentAvailable) => new()
        {
            IsEnabled = settings.IsSelling(),
            PlayModeId = settings.PlayModeId,
            TimeZoneId = settings.TimeZoneId,
            MaxBookingMinutes = settings.MaxBookingMinutes,
            MaxDaysInAdvance = settings.MaxDaysInAdvance,
            MaxUpcomingBookings = settings.MaxUpcomingBookings,
            IsOnlinePaymentAvailable = isOnlinePaymentAvailable,
        };
    }
}

/// <summary>Changes how the club sells court time. The play mode must belong to the club.</summary>
public record UpdateCourtSaleSettings(int ClubId, UpdateCourtSaleSettingsModel Model) : ICommand<CourtSaleSettingsDto>
{
    public class Handler(AppDbContext context, IPaymentAvailability payments) : IRequestHandler<UpdateCourtSaleSettings, CourtSaleSettingsDto>
    {
        public async Task<CourtSaleSettingsDto> Handle(UpdateCourtSaleSettings request, CancellationToken cancellationToken)
        {
            var model = request.Model;
            if (model.PlayModeId is { } playModeId)
            {
                var playMode = await context.PlayModes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playModeId && p.ClubId == request.ClubId, cancellationToken)
                    ?? throw new EntityNotFoundException(typeof(PlayMode));

                if (playMode.CanOverbook)
                    throw new PreconditionException(CourtSaleErrorCode.CourtSalePlayModeCanOverbook, "Bought slots can't use a play mode that overbooks.");

                if (playMode.FixedDuration is not null)
                    throw new PreconditionException(CourtSaleErrorCode.CourtSalePlayModeHasFixedDuration, "Bought slots can't use a play mode with a fixed duration.");
            }

            var settings = await context.CourtSaleSettings.FirstOrDefaultAsync(s => s.ClubId == request.ClubId, cancellationToken);
            if (settings is null)
            {
                settings = new CourtSaleSettings(request.ClubId);
                context.CourtSaleSettings.Add(settings);
            }

            settings.Update(new CourtSaleSettings.SettingsData(
                model.IsEnabled, model.PlayModeId, model.TimeZoneId, model.MaxBookingMinutes, model.MaxDaysInAdvance, model.MaxUpcomingBookings));

            await context.SaveChangesAsync(cancellationToken);
            return GetCourtSaleSettings.Handler.ToDto(settings, await payments.CanAcceptPayments(request.ClubId, cancellationToken));
        }
    }
}

/// <summary>The price rules of the club. Every member may read them: buyers see the prices.</summary>
public record GetCourtPriceRules(int ClubId) : IQuery<List<CourtPriceRuleDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetCourtPriceRules, List<CourtPriceRuleDto>>
    {
        public async Task<List<CourtPriceRuleDto>> Handle(GetCourtPriceRules request, CancellationToken cancellationToken)
        {
            var rules = await context.CourtPriceRules.AsNoTracking().Where(r => r.ClubId == request.ClubId).ToListAsync(cancellationToken);
            return rules
                .OrderBy(r => r.Weekdays.Min(d => ((int)d + 6) % 7)) // Monday first
                .ThenBy(r => r.From)
                .ThenBy(r => r.Id)
                .Select(r => r.ToDto())
                .ToList();
        }
    }
}

/// <summary>Adds a price rule. It must not overlap another rule of the club (<see cref="CourtSaleErrorCode.CourtPriceRulesOverlap"/>).</summary>
public record CreateCourtPriceRule(int ClubId, SaveCourtPriceRuleModel Model) : ICommand<CourtPriceRuleDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateCourtPriceRule, CourtPriceRuleDto>
    {
        public async Task<CourtPriceRuleDto> Handle(CreateCourtPriceRule request, CancellationToken cancellationToken)
        {
            var data = request.Model.ToRuleData();
            await context.EnsureCourtsBelongToClub(request.ClubId, data.CourtIds, cancellationToken);

            var rule = new CourtPriceRule(request.ClubId, data);
            rule.EnsureNoOverlap(await context.GetCourtPriceRules(request.ClubId, cancellationToken));

            context.CourtPriceRules.Add(rule);
            await context.SaveChangesAsync(cancellationToken);
            return rule.ToDto();
        }
    }
}

/// <summary>Changes a price rule. Purchases that were made keep their price.</summary>
public record UpdateCourtPriceRule(int ClubId, int RuleId, SaveCourtPriceRuleModel Model) : ICommand<CourtPriceRuleDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateCourtPriceRule, CourtPriceRuleDto>
    {
        public async Task<CourtPriceRuleDto> Handle(UpdateCourtPriceRule request, CancellationToken cancellationToken)
        {
            var data = request.Model.ToRuleData();
            await context.EnsureCourtsBelongToClub(request.ClubId, data.CourtIds, cancellationToken);

            var rules = await context.GetCourtPriceRules(request.ClubId, cancellationToken);
            var rule = rules.FirstOrDefault(r => r.Id == request.RuleId) ?? throw new EntityNotFoundException(typeof(CourtPriceRule));

            rule.Update(data);
            rule.EnsureNoOverlap(rules);

            await context.SaveChangesAsync(cancellationToken);
            return rule.ToDto();
        }
    }
}

/// <summary>Removes a price rule: its times are no longer for sale. Purchases that were made stay.</summary>
public record DeleteCourtPriceRule(int ClubId, int RuleId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteCourtPriceRule>
    {
        public async Task<Unit> Handle(DeleteCourtPriceRule request, CancellationToken cancellationToken)
        {
            var rule = await context.CourtPriceRules.FirstOrDefaultAsync(r => r.Id == request.RuleId && r.ClubId == request.ClubId, cancellationToken)
                ?? throw new EntityNotFoundException(typeof(CourtPriceRule));

            context.CourtPriceRules.Remove(rule);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
