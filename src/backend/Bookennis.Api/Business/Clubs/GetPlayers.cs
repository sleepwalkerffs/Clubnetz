using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Club;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

/// <summary>
/// The players that can be picked for a booking: the members enrolled in the active season (and the players of clubs that share theirs).
/// <see cref="OnlyMemberIds"/>: a member who is not active only gets themself and the children of their family, never the others.
/// </summary>
public record GetPlayers(IReadOnlySet<int>? OnlyMemberIds = null) : ICommand<GetPlayersResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetPlayers, GetPlayersResult>
    {
        public async Task<GetPlayersResult> Handle(GetPlayers request, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var currentClub = await context.Clubs.SingleRequiredAsync(cancellationToken);

            var clubPlayers = await (from user in context.Users
                                     join member in context.ClubMembers on user.Id equals member.UserId
                                     where context.MemberSeasons.Any(ms => ms.MemberId == member.Id
                                            && context.Seasons.Any(s => s.Id == ms.SeasonId && s.Period.From <= today && today <= s.Period.To))
                                     select new PlayerResult(member.Id, user.FirstName, user.LastName, false, false, user.Id, context.UserProfilePictures.Any(p => p.UserId == user.Id) ? "/api/Profile/picture/" + user.Id : null))
                                     .ToListAsync(cancellationToken);

            if (request.OnlyMemberIds is { } onlyMemberIds)
            {
                var own = await (from user in context.Users
                                 join member in context.ClubMembers on user.Id equals member.UserId
                                 where onlyMemberIds.Contains(member.Id) && member.ClubId == currentClub.Id
                                 select new PlayerResult(member.Id, user.FirstName, user.LastName, false, false, user.Id, context.UserProfilePictures.Any(p => p.UserId == user.Id) ? "/api/Profile/picture/" + user.Id : null))
                                 .ToListAsync(cancellationToken);

                return new GetPlayersResult(own);
            }

            if (currentClub.IsAtpClub)
            {
                var atpPlayers = await (from user in context.Users
                                        join member in context.ClubMembers.IgnoreQueryFilters() on user.Id equals member.UserId
                                        join club in context.Clubs on member.ClubId equals club.Id
                                        where club.IsAtpClub && club.Id != currentClub.Id
                                        select new PlayerResult(member.Id, user.FirstName, user.LastName, false, false, user.Id, context.UserProfilePictures.Any(p => p.UserId == user.Id) ? "/api/Profile/picture/" + user.Id : null))
                                     .ToListAsync(cancellationToken);

                clubPlayers.AddRange(atpPlayers);
                clubPlayers = clubPlayers.DistinctBy(p => p.MemberId).ToList();
            }

            // Guests were removed: former guest members are no players any more
            return new GetPlayersResult(clubPlayers);
        }
    }
}