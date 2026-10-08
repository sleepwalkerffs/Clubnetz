using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Members;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

/// <summary>Booking and season overview of a member for the club administration.</summary>
public record GetMemberOverview(int ClubId, int MemberId) : IQuery<GetMemberOverviewResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetMemberOverview, GetMemberOverviewResult>
    {
        public async Task<GetMemberOverviewResult> Handle(GetMemberOverview request, CancellationToken cancellationToken)
        {
            // Only members of the club the administrator manages
            if (!await context.Set<Member>().AnyAsync(m => m.Id == request.MemberId && m.ClubId == request.ClubId, cancellationToken))
                throw new EntityNotFoundException(typeof(Member), request.MemberId);

            var now = DateTimeOffset.UtcNow;

            var seasons = await context.Seasons
                .Where(s => s.ClubId == request.ClubId)
                .OrderByDescending(s => s.Period.From)
                .ToListAsync(cancellationToken);

            var enrolledSeasonIds = await context.MemberSeasons
                .Where(ms => ms.MemberId == request.MemberId)
                .Select(ms => ms.SeasonId)
                .ToHashSetAsync(cancellationToken);

            var bookings = await context.BookingPlayers
                .Where(bp => bp.MemberId == request.MemberId)
                .Select(bp => new { bp.Booking.Interval.From, bp.Booking.Interval.To })
                .ToListAsync(cancellationToken);

            var played = bookings.Where(b => b.From <= now).ToList();

            var tierBadges = await context.MemberBadges
                .Where(mb => mb.MemberId == request.MemberId)
                .GroupBy(mb => mb.SeasonId)
                .Select(g => new { SeasonId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SeasonId, x => x.Count, cancellationToken);

            var oneTimeBadges = await (
                from award in context.MemberOneTimeBadges
                join badge in context.OneTimeBadges on award.OneTimeBadgeId equals badge.Id
                where award.MemberId == request.MemberId
                group award by badge.SeasonId into g
                select new { SeasonId = g.Key, Count = g.Count() }
            ).ToDictionaryAsync(x => x.SeasonId, x => x.Count, cancellationToken);

            return new GetMemberOverviewResult
            {
                TotalBookings = played.Count,
                TotalHours = Math.Round(played.Sum(b => (b.To - b.From).TotalHours), 1),
                FirstPlayed = played.Count == 0 ? null : played.Min(b => b.From),
                LastPlayed = played.Count == 0 ? null : played.Max(b => b.From),
                UpcomingBookings = bookings.Count - played.Count,
                Seasons = seasons.Select(season =>
                {
                    // Seasons are stored as dates, bookings are compared in UTC like the leaderboard does
                    var from = new DateTimeOffset(season.Period.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                    var to = new DateTimeOffset(season.Period.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                    var seasonBookings = played.Where(b => b.From >= from && b.From < to).ToList();

                    return new MemberSeasonOverview
                    {
                        SeasonId = season.Id,
                        StartDate = season.Period.From,
                        EndDate = season.Period.To,
                        IsEnrolled = enrolledSeasonIds.Contains(season.Id),
                        Bookings = seasonBookings.Count,
                        Hours = Math.Round(seasonBookings.Sum(b => (b.To - b.From).TotalHours), 1),
                        Badges = tierBadges.GetValueOrDefault(season.Id) + oneTimeBadges.GetValueOrDefault(season.Id),
                    };
                }).ToList(),
            };
        }
    }
}
