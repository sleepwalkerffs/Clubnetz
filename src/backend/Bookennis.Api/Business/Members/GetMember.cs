using Bookennis.Api.Data;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Members;
using Bookennis.Shared.Controller.Shared;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

public record GetMember(int MemberId) : ICommand<MembersDetailResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetMember, MembersDetailResult>
    {
        public async Task<MembersDetailResult> Handle(GetMember request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var result = await (from user in context.Users
                                join member in context.Set<Member>().IgnoreQueryFilters() on user.Id equals member.UserId
                                join club in context.Clubs.IgnoreQueryFilters() on member.ClubId equals club.Id

                                join familyMember in context.FamilyMembers on member.Id equals familyMember.MemberId into tmpFamilyMember
                                from familyMember in tmpFamilyMember.DefaultIfEmpty()

                                where member.Id == request.MemberId

                                select new MembersDetailResult
                                {
                                    MemberId = member.Id,
                                    FamilyId = familyMember.FamilyId,
                                    Email = user.Email,
                                    FullName = user.FullName,
                                    FirstName = user.FirstName,
                                    LastName = user.LastName,
                                    Gender = (Gender)user.Gender,
                                    Birthday = user.Birthday,
                                    Roles = member.UserRoles.Select(x => (Bookennis.Shared.Controller.Shared.MemberRole)x).ToArray(),
                                    BookingsPerWeek = member.BookingsPerWeek == null ? club.ConcurrentAllowedBookings : member.BookingsPerWeek!.Value,
                                    AllowedSeasonIds = context.MemberSeasons
                                        .Where(ms => ms.MemberId == member.Id)
                                        .Select(ms => ms.SeasonId)
                                        .ToArray(),
                                    ProfilePictureUrl = context.UserProfilePictures.Any(p => p.UserId == user.Id) ? "/api/Profile/picture/" + user.Id : null,
                                    ContactEmail = user.Email ?? (from childFm in context.FamilyMembers.OfType<ChildFamilyMember>()
                                                                  where childFm.MemberId == member.Id
                                                                  from parentFm in context.FamilyMembers.OfType<ParentFamilyMember>()
                                                                      .Where(pf => pf.ParentFamilyId == childFm.ChildFamilyId)
                                                                  join parentMember in context.Set<Member>().IgnoreQueryFilters() on parentFm.MemberId equals parentMember.Id
                                                                  join parentUser in context.Users on parentMember.UserId equals parentUser.Id
                                                                  where parentUser.Email != null
                                                                  orderby parentUser.LastName, parentUser.FirstName
                                                                  select parentUser.Email).FirstOrDefault(),
                                    LastPlayed = context.BookingPlayers
                                        .Where(bp => bp.MemberId == member.Id && bp.Booking.Interval.From <= now)
                                        .Max(bp => (DateTimeOffset?)bp.Booking.Interval.From),
                                }).FirstOrDefaultAsync(cancellationToken)
                             ?? throw new EntityNotFoundException(typeof(Member), request.MemberId);

            return result;
        }
    }
}