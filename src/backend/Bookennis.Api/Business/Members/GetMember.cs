using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Members;
using Bookennis.Shared.Controller.Memberships;
using Bookennis.Shared.Controller.Shared;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

/// <summary>
/// The full record of a member of the club, with email, birthday, roles and seasons. The controller only hands it to the member
/// themself and to those who manage the members; everybody else gets <see cref="GetMemberProfile"/>.
/// </summary>
public record GetMember(int MemberId) : ICommand<MembersDetailResult>
{
    public class Handler(AppDbContext context, ITenantService tenantService) : IRequestHandler<GetMember, MembersDetailResult>
    {
        public async Task<MembersDetailResult> Handle(GetMember request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var currentClubId = tenantService.GetTenantId();

            var result = await (from user in context.Users
                                join member in context.Set<Member>().IgnoreQueryFilters() on user.Id equals member.UserId
                                join club in context.Clubs.IgnoreQueryFilters() on member.ClubId equals club.Id

                                join familyMember in context.FamilyMembers on member.Id equals familyMember.MemberId into tmpFamilyMember
                                from familyMember in tmpFamilyMember.DefaultIfEmpty()

                                // IgnoreQueryFilters switches the tenant filter off for the whole query, so the club is checked here.
                                // Players of other clubs (ATP) are never returned with their full record, see GetMemberProfile.
                                where member.Id == request.MemberId
                                   && (currentClubId == null || member.ClubId == currentClubId)

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

            // Where each enrollment came from: by hand, or from a membership order (and how that one was paid)
            var enrollments = await (
                from memberSeason in context.MemberSeasons
                where memberSeason.MemberId == result.MemberId
                select new
                {
                    memberSeason.SeasonId,
                    memberSeason.MembershipOrderId,
                    Method = context.MembershipOrders.IgnoreQueryFilters()
                        .Where(o => o.Id == memberSeason.MembershipOrderId)
                        .Select(o => o.PaymentMethod)
                        .FirstOrDefault(),
                }
            ).ToListAsync(cancellationToken);

            result.PaidSeasonIds = enrollments.Where(e => e.MembershipOrderId is not null).Select(e => e.SeasonId).ToArray();
            result.SeasonEnrollments = enrollments
                .Select(e => new MemberSeasonEnrollment(
                    e.SeasonId,
                    e.MembershipOrderId is null ? MembershipEnrollmentSource.ByHand
                    : e.Method == Domain.Memberships.MembershipPaymentMethod.Online ? MembershipEnrollmentSource.PaidOnline
                    : MembershipEnrollmentSource.MarkedAsPaid))
                .ToList();

            return result;
        }
    }
}