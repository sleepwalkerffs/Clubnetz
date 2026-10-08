using Bookennis.Api.Data;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Members;
using Microsoft.EntityFrameworkCore;
using User = Bookennis.Domain.User.User;

namespace Bookennis.Api.Business.Members;

/// <summary>
/// Builds the query of the club members list. It is shared by the list and the email export,
/// so that "copy emails" always returns exactly the emails of the listed members.
/// </summary>
public static class MemberFilterQuery
{
    public sealed class Row
    {
        public required User User { get; init; }
        public required ClubMember Member { get; init; }

        /// <summary>The member's email or, for children without an email, the email of a parent.</summary>
        public required string? ContactEmail { get; init; }
    }

    public static async Task<IQueryable<Row>> Build(AppDbContext context, MemberFilter filter, CancellationToken cancellationToken)
    {
        var searchTerm = QueryUtil.Escape(filter.SearchTerm);
        var roles = filter.Roles is { Length: > 0 } ? filter.Roles.Select(r => (Domain.Members.MemberRole)(int)r).ToArray() : null;
        var genders = filter.Genders is { Length: > 0 } ? filter.Genders.Select(g => (Domain.User.Gender)(int)g).ToArray() : null;
        var inSeasonIds = filter.InSeasonIds is { Length: > 0 } ? filter.InSeasonIds.Distinct().ToArray() : null;
        var notInSeasonIds = filter.NotInSeasonIds is { Length: > 0 } ? filter.NotInSeasonIds.Distinct().ToArray() : null;

        var newMemberIds = filter.NewMembersOnly
            ? await MemberQueryHelper.GetNewMemberIds(context, cancellationToken)
            : null;

        var query =
            from user in context.Users
            join member in context.ClubMembers on user.Id equals member.UserId
            let parentEmail = (from childFm in context.FamilyMembers.OfType<ChildFamilyMember>()
                               where childFm.MemberId == member.Id
                               from parentFm in context.FamilyMembers.OfType<ParentFamilyMember>()
                                   .Where(pf => pf.ParentFamilyId == childFm.ChildFamilyId)
                               join parentMember in context.ClubMembers on parentFm.MemberId equals parentMember.Id
                               join parentUser in context.Users on parentMember.UserId equals parentUser.Id
                               where parentUser.Email != null
                               orderby parentUser.LastName, parentUser.FirstName
                               select parentUser.Email).FirstOrDefault()
            select new Row { User = user, Member = member, ContactEmail = user.Email ?? parentEmail };

        if (searchTerm is not null)
            query = query.Where(r => EF.Functions.ILike(r.User.FullName, searchTerm) || EF.Functions.ILike(r.ContactEmail!, searchTerm));

        if (roles is not null)
            query = query.Where(r => r.Member.UserRoles.Any(role => roles.Contains(role)));

        if (newMemberIds is not null)
            query = query.Where(r => newMemberIds.Contains(r.Member.Id));

        if (inSeasonIds is not null)
            query = query.Where(r => context.MemberSeasons.Count(ms => ms.MemberId == r.Member.Id && inSeasonIds.Contains(ms.SeasonId)) == inSeasonIds.Length);

        if (notInSeasonIds is not null)
            query = query.Where(r => !context.MemberSeasons.Any(ms => ms.MemberId == r.Member.Id && notInSeasonIds.Contains(ms.SeasonId)));

        if (filter.NoBookingsInSeasonId is { } noBookingsSeasonId)
        {
            var season = await context.Seasons.FirstOrDefaultAsync(s => s.Id == noBookingsSeasonId, cancellationToken);
            if (season is not null)
            {
                var from = new DateTimeOffset(season.Period.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var to = new DateTimeOffset(season.Period.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var now = DateTimeOffset.UtcNow;
                if (now < to)
                    to = now;

                query = query.Where(r => !context.BookingPlayers.Any(bp => bp.MemberId == r.Member.Id
                                                                           && bp.Booking.Interval.From >= from
                                                                           && bp.Booking.Interval.From < to));
            }
        }

        if (genders is not null)
            query = query.Where(r => genders.Contains(r.User.Gender));

        if (filter.AgeGroups is { Length: > 0 } ageGroups)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var kids = ageGroups.Contains(AgeGroup.Kids);
            var teenagers = ageGroups.Contains(AgeGroup.Teenagers);
            var adults = ageGroups.Contains(AgeGroup.Adults);
            var seniors = ageGroups.Contains(AgeGroup.Seniors);

            // Born after these dates means younger than 13, 18 or 35 years
            var age13 = today.AddYears(-13);
            var age18 = today.AddYears(-18);
            var age35 = today.AddYears(-35);

            query = query.Where(r => (kids && r.User.Birthday > age13)
                                     || (teenagers && r.User.Birthday <= age13 && r.User.Birthday > age18)
                                     || (adults && r.User.Birthday <= age18 && r.User.Birthday > age35)
                                     || (seniors && r.User.Birthday <= age35));
        }

        if (filter.HasEmail is { } hasEmail)
            query = hasEmail ? query.Where(r => r.ContactEmail != null) : query.Where(r => r.ContactEmail == null);

        return query;
    }
}
