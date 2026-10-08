using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Events;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Members;
using Bookennis.Domain.Members.Events;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members.DomainEventHandlers;

public class SendMemberSeasonActivatedEmailHandler(
    AppDbContext context,
    IMediator mediator) : INotificationHandler<DomainEvent<MemberActivatedForSeasonDomainEvent>>
{
    public async Task Handle(DomainEvent<MemberActivatedForSeasonDomainEvent> notification, CancellationToken cancellationToken)
    {
        var memberId = notification.Event.MemberId;
        var seasonId = notification.Event.SeasonId;

        var memberInfo = await (
            from member in context.Set<Member>()
            join user in context.Users on member.UserId equals user.Id
            where member.Id == memberId
            select new { user.Email, user.FirstName, user.LastName, user.Language, member.ClubId }
        ).SingleOrDefaultAsync(cancellationToken);

        if (memberInfo?.Email is null)
            return;

        var activatedSeason = await context.Seasons
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        if (activatedSeason is null)
            return;

        var previousSeason = await context.Seasons
            .IgnoreQueryFilters()
            .Where(s => s.ClubId == memberInfo.ClubId && s.Id != activatedSeason.Id && s.Period.To < activatedSeason.Period.From)
            .OrderByDescending(s => s.Period.To)
            .FirstOrDefaultAsync(cancellationToken);

        var wasActiveInPreviousSeason = false;
        if (previousSeason is not null)
        {
            wasActiveInPreviousSeason = await context.MemberSeasons
                .AnyAsync(ms => ms.MemberId == memberId && ms.SeasonId == previousSeason.Id, cancellationToken);
        }

        var culture = memberInfo.Language.ToCultureInfo();
        var memberVariables = new MemberVariables(memberInfo.FirstName, memberInfo.LastName);
        var season = ClubEmailCatalog.FormatSeason(activatedSeason.Period.From, activatedSeason.Period.To, culture);

        ClubEmailVariables variables = wasActiveInPreviousSeason
            ? new SeasonActivatedEmailVariables(memberVariables, season)
            : new WelcomeEmailVariables(memberVariables, season);

        await mediator.Send(
            new SendClubEmail(
                memberInfo.ClubId,
                wasActiveInPreviousSeason ? ClubEmailType.SeasonActivated : ClubEmailType.Welcome,
                memberInfo.Email,
                memberVariables.FullName,
                memberInfo.Language,
                variables),
            cancellationToken);
    }
}
