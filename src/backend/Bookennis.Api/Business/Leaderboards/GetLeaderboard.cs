using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Leaderboards;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Leaderboards;

public record GetLeaderboard(int UserId) : IQuery<GetLeaderboardResult>
{
    private const int MaxEntries = 25;

    /// <summary>Rank changes are shown compared to the leaderboard of this many days ago.</summary>
    private const int RankChangeDays = 7;

    private sealed record MemberInfo(string FirstName, string LastName, int UserId, bool HasProfilePicture, string? DisplayBadgeImageUrl, int? DisplayBadgeLevel);

    private sealed record BookingRow(DateTimeOffset From, DateTimeOffset To, List<int> MemberIds, int? SeasonId = null);

    private sealed record Totals(double Hours, int Matches);

    public class Handler(AppDbContext context) : IRequestHandler<GetLeaderboard, GetLeaderboardResult>
    {
        public async Task<GetLeaderboardResult> Handle(GetLeaderboard request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var rankChangeCutoff = now.AddDays(-RankChangeDays);

            var currentMemberIds = await context.Set<Member>()
                .Where(m => m.UserId == request.UserId)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            var chargingPlayModeIds = await context.PlayModes
                .Where(pm => pm.IsChargingBookingSubscription)
                .Select(pm => pm.Id)
                .ToListAsync(cancellationToken);

            var seasons = await context.Seasons
                .OrderByDescending(s => s.Period.From)
                .ToListAsync(cancellationToken);

            var optOuts = await context.MemberSeasons
                .Where(ms => ms.LeaderboardOptOut)
                .Select(ms => new { ms.MemberId, ms.SeasonId })
                .ToListAsync(cancellationToken);

            var currentMemberOptOutSeasonIds = optOuts
                .Where(o => currentMemberIds.Contains(o.MemberId))
                .Select(o => o.SeasonId)
                .ToHashSet();

            // Only bookings that already took place count, future bookings are not played yet
            var bookings = (await (
                from booking in context.Bookings
                where chargingPlayModeIds.Contains(booking.PlayModeId) && booking.Interval.From <= now
                select new
                {
                    booking.Interval.From,
                    booking.Interval.To,
                    MemberIds = context.BookingPlayers
                        .Where(bp => bp.BookingEntryId == booking.Id)
                        .Select(bp => bp.MemberId)
                        .ToList()
                }
            ).ToListAsync(cancellationToken))
                .ConvertAll(b => new BookingRow(b.From, b.To, b.MemberIds));

            // Only the members that actually played are needed (members of other clubs can take part, e.g. ATP players)
            var playingMemberIds = bookings.SelectMany(b => b.MemberIds).Distinct().ToList();

            var memberLookupBase = await (
                from member in context.Set<Member>().IgnoreQueryFilters()
                join user in context.Users on member.UserId equals user.Id
                where playingMemberIds.Contains(member.Id)
                select new { member.Id, user.FirstName, user.LastName, UserId = user.Id, HasProfilePicture = context.UserProfilePictures.Any(p => p.UserId == user.Id) }
            ).ToListAsync(cancellationToken);

            var displayBadgeByMember = await (
                from settings in context.MemberBadgeSettings
                join mb in context.MemberBadges on settings.DisplayBadgeId equals mb.Id
                join bt in context.BadgeTiers on mb.BadgeTierId equals bt.Id
                where playingMemberIds.Contains(settings.MemberId)
                select new { settings.MemberId, TierId = bt.Id, bt.ClubId, bt.Level, HasImage = context.BadgeTierImages.Any(i => i.BadgeTierId == bt.Id) }
            ).ToDictionaryAsync(x => x.MemberId, cancellationToken);

            var memberDict = memberLookupBase.ToDictionary(
                m => m.Id,
                m =>
                {
                    displayBadgeByMember.TryGetValue(m.Id, out var badge);
                    var badgeImageUrl = badge is { HasImage: true } ? $"/api/Clubs/{badge.ClubId}/BadgeTiers/{badge.TierId}/image" : null;
                    return new MemberInfo(m.FirstName, m.LastName, m.UserId, m.HasProfilePicture, badgeImageUrl, badge?.Level);
                });

            var seasonRanges = seasons.ToDictionary(
                s => s.Id,
                s => (From: new DateTimeOffset(s.Period.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                      To: new DateTimeOffset(s.Period.To.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)));

            bookings = bookings.ConvertAll(b => b with
            {
                SeasonId = seasons.FirstOrDefault(season => b.From >= seasonRanges[season.Id].From && b.From <= seasonRanges[season.Id].To)?.Id,
            });

            var seasonLeaderboards = new List<SeasonLeaderboardResult>();

            foreach (var season in seasons)
            {
                var seasonOptedOutMemberIds = optOuts
                    .Where(o => o.SeasonId == season.Id)
                    .Select(o => o.MemberId)
                    .ToHashSet();

                var seasonBookings = bookings.Where(b => b.SeasonId == season.Id).ToList();

                bool Include(int memberId, BookingRow _) => !seasonOptedOutMemberIds.Contains(memberId);

                var data = BuildLeaderboardData(
                    seasonBookings.Count(b => b.MemberIds.Any(memberId => Include(memberId, b))),
                    Aggregate(seasonBookings, Include),
                    Aggregate(seasonBookings.Where(b => b.From <= rankChangeCutoff), Include),
                    memberDict,
                    currentMemberIds);

                seasonLeaderboards.Add(new SeasonLeaderboardResult
                {
                    SeasonId = season.Id,
                    StartDate = season.Period.From,
                    EndDate = season.Period.To,
                    IsOptedOut = currentMemberOptOutSeasonIds.Contains(season.Id),
                    Data = data,
                });
            }

            // All-time: exclude bookings from seasons where the member opted out
            var optedOutSeasonsByMember = optOuts
                .GroupBy(o => o.MemberId)
                .ToDictionary(g => g.Key, g => g.Select(o => o.SeasonId).ToHashSet());

            bool IncludeAllTime(int memberId, BookingRow booking)
                => booking.SeasonId is not { } seasonId
                   || !optedOutSeasonsByMember.TryGetValue(memberId, out var optedOutSeasons)
                   || !optedOutSeasons.Contains(seasonId);

            var allTimeData = BuildLeaderboardData(
                bookings.Count(b => b.MemberIds.Any(memberId => IncludeAllTime(memberId, b))),
                Aggregate(bookings, IncludeAllTime),
                Aggregate(bookings.Where(b => b.From <= rankChangeCutoff), IncludeAllTime),
                memberDict,
                currentMemberIds);

            return new GetLeaderboardResult
            {
                SeasonLeaderboards = seasonLeaderboards,
                AllTime = allTimeData,
            };
        }

        private static Dictionary<int, Totals> Aggregate(IEnumerable<BookingRow> bookings, Func<int, BookingRow, bool> include)
        {
            var totals = new Dictionary<int, Totals>();
            foreach (var booking in bookings)
            {
                var hours = (booking.To - booking.From).TotalHours;
                foreach (var memberId in booking.MemberIds.Where(memberId => include(memberId, booking)))
                {
                    var current = totals.GetValueOrDefault(memberId, new Totals(0, 0));
                    totals[memberId] = new Totals(current.Hours + hours, current.Matches + 1);
                }
            }

            return totals;
        }

        private static List<(int MemberId, Totals Totals, int Rank)> Rank(Dictionary<int, Totals> totals)
            => totals
                .OrderByDescending(t => t.Value.Hours)
                .ThenBy(t => t.Key)
                .Select((t, index) => (t.Key, t.Value, index + 1))
                .ToList();

        private static LeaderboardData BuildLeaderboardData(
            int totalMatches,
            Dictionary<int, Totals> totals,
            Dictionary<int, Totals> previousTotals,
            Dictionary<int, MemberInfo> memberLookup,
            List<int> currentMemberIds)
        {
            var previousRanks = Rank(previousTotals).ToDictionary(r => r.MemberId, r => r.Rank);

            var ranked = Rank(totals)
                .Select(r =>
                {
                    memberLookup.TryGetValue(r.MemberId, out var info);
                    return new LeaderboardEntry
                    {
                        Rank = r.Rank,
                        MemberId = r.MemberId,
                        FirstName = info?.FirstName ?? "Unknown",
                        LastName = info?.LastName ?? "",
                        TotalHours = Math.Round(r.Totals.Hours, 1),
                        Matches = r.Totals.Matches,
                        RankChange = previousRanks.TryGetValue(r.MemberId, out var previousRank) ? previousRank - r.Rank : null,
                        IsCurrentMember = currentMemberIds.Contains(r.MemberId),
                        ProfilePictureUrl = info is { HasProfilePicture: true } ? $"/api/Profile/picture/{info.UserId}" : null,
                        DisplayBadgeImageUrl = info?.DisplayBadgeImageUrl,
                        DisplayBadgeLevel = info?.DisplayBadgeLevel,
                    };
                })
                .ToList();

            var currentMemberEntry = ranked.FirstOrDefault(e => e.IsCurrentMember);
            var entryAbove = currentMemberEntry is { Rank: > 1 } ? ranked[currentMemberEntry.Rank - 2] : null;

            return new LeaderboardData
            {
                Entries = ranked.Take(MaxEntries).ToList(),
                CurrentMemberRank = currentMemberEntry?.Rank,
                CurrentMemberDuration = currentMemberEntry?.TotalHours,
                CurrentMemberEntry = currentMemberEntry,
                HoursToNextRank = entryAbove is null ? null : Math.Round(entryAbove.TotalHours - currentMemberEntry!.TotalHours, 1),
                TotalPlayers = ranked.Count,
                TotalHours = Math.Round(totals.Values.Sum(t => t.Hours), 1),
                TotalMatches = totalMatches,
            };
        }
    }
}
