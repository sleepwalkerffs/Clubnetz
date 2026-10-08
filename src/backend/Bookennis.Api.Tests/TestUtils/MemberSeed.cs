using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;

namespace Bookennis.Api.Tests.TestUtils;

/// <summary>Seeds users, members, families and bookings in the test club for membership/account removal tests.</summary>
internal static class MemberSeed
{
    public static async Task<(int UserId, int MemberId)> AddUserWithMember(AppDbContext context, string email, MemberRole role = MemberRole.User)
    {
        var user = new User(email, email, "First", email.Split('@')[0], new DateOnly(1990, 1, 1), Gender.Female);
        context.Add(user);
        await context.SaveChangesAsync();

        var member = new ClubMember(user.Id, TestDataSeed.ClubId, [role]);
        context.Add(member);
        await context.SaveChangesAsync();

        return (user.Id, member.Id);
    }

    public static async Task<(int UserId, int MemberId)> AddChild(AppDbContext context, int ownerUserId, string firstName)
    {
        var child = new User(firstName, "Child", new DateOnly(2015, 1, 1), Gender.Male, ownerUserId);
        context.Add(child);
        await context.SaveChangesAsync();

        var member = new ClubMember(child.Id, TestDataSeed.ClubId, [MemberRole.User]);
        context.Add(member);
        await context.SaveChangesAsync();

        return (child.Id, member.Id);
    }

    public static async Task<int> AddFamily(AppDbContext context, List<int> parentMemberIds, List<int> childMemberIds)
    {
        var family = new Family(TestDataSeed.ClubId, parentMemberIds, childMemberIds);
        context.Add(family);
        await context.SaveChangesAsync();
        return family.Id;
    }

    public static async Task<int> AddBooking(AppDbContext context, DateTimeOffset from, params int[] memberIds)
    {
        var playModeId = context.PlayModes.First(p => p.ClubId == TestDataSeed.ClubId).Id;
        var booking = new Booking(TestDataSeed.ClubId, TestDataSeed.Court1Id, playModeId, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, [.. memberIds]);
        context.Add(booking);
        await context.SaveChangesAsync();
        return booking.Id;
    }
}
