using Bookennis.Api.Business.CourtSales;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.CourtSales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtSales;

/// <summary>A member enrolled for tennis buys time on a padel court (and the other way round), but not on the courts of their own sport.</summary>
public class CourtSaleSportTests(TestFixture fixture) : CourtSaleTestBase(fixture)
{
    [Fact]
    public async Task Options_EnrolledForOneOfTwoSports_CanBuy()
    {
        var (userId, memberId) = await SeedBuyer(credits: 1);
        await SeedPadelCourtAndEnroll(memberId, [Sport.Tennis]);

        var options = await SendAsync(new GetCourtSaleOptions(TestDataSeed.ClubId, userId));

        options.CanBuy.Should().BeTrue();
        options.Reason.Should().BeNull();
    }

    [Fact]
    public async Task Options_EnrolledForEverySportOfTheClub_AlreadyEnrolled()
    {
        var (userId, memberId) = await SeedBuyer(credits: 1);
        await SeedPadelCourtAndEnroll(memberId, [Sport.Tennis, Sport.Padel]);

        (await SendAsync(new GetCourtSaleOptions(TestDataSeed.ClubId, userId))).Reason.Should().Be(CourtSaleUnavailableReason.AlreadyEnrolled);
    }

    [Fact]
    public async Task Purchase_CourtOfAnotherSport_IsSold()
    {
        var (userId, memberId) = await SeedBuyer(credits: 1);
        await SeedPadelCourtAndEnroll(memberId, [Sport.Tennis]);

        var result = await SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court2Id, CourtSaleSeed.Tomorrow(60)));

        (await Purchase(result.PurchaseId)).CreditUsed.Should().BeTrue();
    }

    [Fact]
    public async Task Purchase_CourtOfTheOwnSport_BooksForFreeInstead()
    {
        var (userId, memberId) = await SeedBuyer(credits: 1);
        await SeedPadelCourtAndEnroll(memberId, [Sport.Tennis]);

        var act = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtPurchaseErrorCode.AlreadyEnrolled));
    }

    /// <summary>Court 2 becomes a padel court, the buyer is enrolled in the active season for <paramref name="sports"/>.</summary>
    private Task SeedPadelCourtAndEnroll(int memberId, Sport[] sports) => QueryAsync(async ctx =>
    {
        (await ctx.Courts.SingleAsync(c => c.Id == TestDataSeed.Court2Id)).SetSport(Sport.Padel);
        ctx.MemberSeasons.Add(new MemberSeason(memberId, await ctx.Seasons.Select(s => s.Id).SingleAsync(), sports: sports));
        await ctx.SaveChangesAsync();
        return 0;
    });
}
