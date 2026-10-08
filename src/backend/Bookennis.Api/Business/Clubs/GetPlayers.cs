using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Club;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record GetPlayers : ICommand<GetPlayersResult>
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

            var guestPlayers = await (from user in context.Users
                                      join member in context.GuestMembers on user.Id equals member.UserId
                                      where member.IsAllowedToBook
                                      select new PlayerResult(member.Id, user.FirstName, user.LastName, true, false, user.Id, context.UserProfilePictures.Any(p => p.UserId == user.Id) ? "/api/Profile/picture/" + user.Id : null))
                                      .ToListAsync(cancellationToken);

            return new GetPlayersResult(clubPlayers, guestPlayers);
        }
    }
}