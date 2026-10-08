using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubProfile;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubProfile;

public record GetBookingOptions(int UserId, bool IsGuestSession) : IQuery<GetBookingOptionsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetBookingOptions, GetBookingOptionsResult>
    {
        public async Task<GetBookingOptionsResult> Handle(GetBookingOptions request, CancellationToken cancellationToken)
        {
            var memberQuery = context.Set<Member>()
                .Where(m => m.UserId == request.UserId);

            memberQuery = request.IsGuestSession
                ? memberQuery.Where(m => m.MemberType == MemberType.GuestMember)
                : memberQuery.Where(m => m.MemberType == MemberType.ClubMember);

            var member = await memberQuery.FirstOrDefaultAsync(cancellationToken);

            // Fallback: if no member found with specific type, try any
            member ??= await context.Set<Member>()
                .Where(m => m.UserId == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (member is null)
                return new GetBookingOptionsResult { IsAllowedToBook = false };

            if (member.MemberType == MemberType.GuestMember)
            {
                var availableBookings = await context.GuestCards
                    .Where(x => x.GuestMemberId == member.Id)
                    .SumAsync(x => x.PurchasedBookings, cancellationToken);

                var consumedBookings = await context.BookingPlayers
                    .Include(x => x.Booking)
                    .Where(x => x.MemberId == member.Id)
                    .CountAsync(cancellationToken);

                return new GetBookingOptionsResult
                {
                    IsAllowedToBook = member.IsAllowedToBook && availableBookings > consumedBookings
                };
            }

            // ClubMember: check season membership
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var activeSeason = await context.Seasons
                .Where(s => s.Period.From <= today && today <= s.Period.To)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            var isInActiveSeason = activeSeason != null
                && await context.MemberSeasons
                    .AnyAsync(ms => ms.MemberId == member.Id && ms.SeasonId == activeSeason.Id, cancellationToken);

            if (!isInActiveSeason && activeSeason != null)
            {
                // Check if any of the member's children are in the active season
                var childMemberIds = await context.Families
                    .Where(f => f.Parents.Any(p => p.MemberId == member.Id))
                    .SelectMany(f => f.Children.Select(c => c.MemberId))
                    .ToListAsync(cancellationToken);

                if (childMemberIds.Count > 0)
                {
                    var eligibleChildIds = await context.MemberSeasons
                        .Where(ms => childMemberIds.Contains(ms.MemberId) && ms.SeasonId == activeSeason.Id)
                        .Select(ms => ms.MemberId)
                        .ToListAsync(cancellationToken);

                    if (eligibleChildIds.Count > 0)
                    {
                        return new GetBookingOptionsResult
                        {
                            IsAllowedToBook = true,
                            IsBookingOnlyForChildren = true,
                            EligibleChildMemberIds = eligibleChildIds
                        };
                    }
                }
            }

            return new GetBookingOptionsResult
            {
                IsAllowedToBook = isInActiveSeason
            };
        }
    }
}