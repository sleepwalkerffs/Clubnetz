using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;

namespace Bookennis.Api.Tests.Business.Memberships;

/// <summary>
/// Test data for memberships. The seeded club has two members: Member1 (user "us er", born 2000, no special role)
/// and Member2 (the admin).
/// </summary>
internal static class MembershipSeed
{
    public static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public static MembershipPlan.PlanData Data(string name = "Adult", int priceCents = 12000) => new(
        name, null, priceCents, 1, false, null, null, false, false, false, true, null, null, true);

    public static MembershipPlan.PlanData FamilyData(int adults = 2, int priceCents = 21500)
        => Data("Family", priceCents) with { MaxAdults = adults, IncludesChildren = true };

    /// <summary>The running season of the club.</summary>
    public static Task<Season> SeedSeason(AppDbContext context) => SeedSeason(context, Today.AddDays(-30), Today.AddDays(200));

    public static async Task<Season> SeedSeason(AppDbContext context, DateOnly from, DateOnly to, int clubId = TestDataSeed.ClubId)
    {
        var season = new Season(clubId, new DateOnlyInterval(from, to));
        context.Add(season);
        await context.SaveChangesAsync();
        return season;
    }

    public static async Task<MembershipPlan> SeedPlan(AppDbContext context, int seasonId, MembershipPlan.PlanData? data = null, int sortOrder = 1)
    {
        var plan = new MembershipPlan(TestDataSeed.ClubId, seasonId, data ?? Data(), sortOrder);
        context.Add(plan);
        await context.SaveChangesAsync();
        return plan;
    }

    public static async Task<MembershipOrder> SeedOrder(AppDbContext context, MembershipPlan plan, int? payerMemberId, params int[] memberIds)
    {
        var order = new MembershipOrder(plan, payerMemberId, memberIds);
        context.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    /// <summary>A club member with an own login.</summary>
    public static async Task<int> AddMember(AppDbContext context, string firstName, int birthYear = 1985, MemberRole[]? roles = null, Language language = Language.German)
    {
        var email = $"{firstName.ToLowerInvariant()}@membership.test";
        var user = new User(email, email, firstName, "Tester", new DateOnly(birthYear, 6, 15), Gender.Female, language: language);
        context.Add(user);
        await context.SaveChangesAsync();

        var member = new ClubMember(user.Id, TestDataSeed.ClubId, roles ?? [MemberRole.User]);
        context.Add(member);
        await context.SaveChangesAsync();
        return member.Id;
    }

    /// <summary>A child without a login that belongs to the user of a parent.</summary>
    public static async Task<int> AddChild(AppDbContext context, string firstName, int birthYear, int parentUserId = TestDataSeed.UserId)
    {
        var user = new User(firstName, "Tester", new DateOnly(birthYear, 6, 15), Gender.Male, parentUserId);
        context.Add(user);
        await context.SaveChangesAsync();

        var member = new ClubMember(user.Id, TestDataSeed.ClubId, [MemberRole.User]);
        context.Add(member);
        await context.SaveChangesAsync();
        return member.Id;
    }

    public static async Task<int> AddFamily(AppDbContext context, int[] parentMemberIds, params int[] childMemberIds)
    {
        var family = new Family(TestDataSeed.ClubId, [.. parentMemberIds], [.. childMemberIds]);
        context.Add(family);
        await context.SaveChangesAsync();
        return family.Id;
    }

    public static async Task Enroll(AppDbContext context, int seasonId, params int[] memberIds)
    {
        foreach (var memberId in memberIds)
            context.Add(new MemberSeason(memberId, seasonId));

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Member1 as the parent of a family with a partner (born 1987) and two children (born 2012 and 2019), in the running season.
    /// </summary>
    public static async Task<SeededFamily> SeedFamily(AppDbContext context)
    {
        var season = await SeedSeason(context);
        var partner = await AddMember(context, "Partner", 1987);
        var teen = await AddChild(context, "Teen", 2012);
        var kid = await AddChild(context, "Kid", 2019);
        var familyId = await AddFamily(context, [TestDataSeed.Member1Id, partner], teen, kid);

        return new SeededFamily(season.Id, familyId, TestDataSeed.Member1Id, partner, teen, kid);
    }
}

public sealed record SeededFamily(int SeasonId, int FamilyId, int Parent, int Partner, int Teen, int Kid)
{
    public int[] Everybody => [Parent, Partner, Teen, Kid];
}
