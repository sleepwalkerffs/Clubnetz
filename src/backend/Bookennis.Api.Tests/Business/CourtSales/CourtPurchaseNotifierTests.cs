using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtSales;

public class CourtPurchaseNotifierTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task Confirmed_GoesToTheBuyerWithCourtTimeAndHowItWasPaid()
    {
        var purchaseId = await Seed(creditUsed: true);

        var emails = await Run(purchaseId, (notifier, purchase) => notifier.Confirmed(purchase, CancellationToken.None));

        var email = emails.Should().ContainSingle().Subject;
        email.Should().BeEquivalentTo(new { ClubId = TestDataSeed.ClubId, Type = ClubEmailType.CourtPurchaseConfirmed, Recipient = "buyer@test.com" }, o => o.ExcludingMissingMembers());
        var purchase = email.Variables.Should().BeOfType<CourtPurchaseEmailVariables>().Subject.Purchase;
        purchase.Court.Should().Be("Court 1");
        purchase.Time.Should().Be("10:00 – 11:00", "the time of the club's time zone");
        purchase.Paid.Should().Be("1 Buchungsguthaben");
        purchase.Price.Should().Be("€ 20,00");
        purchase.Url.Should().StartWith($"http://notvalid.com/clubs/{TestDataSeed.ClubId}/booking?date=");
    }

    [Fact]
    public async Task PaymentFailed_UsesItsOwnType()
    {
        var purchaseId = await Seed(creditUsed: false);

        var emails = await Run(purchaseId, (notifier, purchase) => notifier.PaymentFailed(purchase, CancellationToken.None));

        emails.Should().ContainSingle().Which.Type.Should().Be(ClubEmailType.CourtPurchaseFailed);
    }

    [Fact]
    public async Task NeedsAttention_GoesToTreasurersAndAdminsWithTheReasonAndTheBuyer()
    {
        var purchaseId = await Seed(creditUsed: false);
        await QueryAsync(async ctx =>
        {
            await TestUtils.MemberSeed.AddUserWithMember(ctx, "treasurer@test.com", MemberRole.Treasurer);
            return 0;
        });

        var emails = await Run(purchaseId, (notifier, purchase) => notifier.NeedsAttention(purchase, CourtPurchaseAttentionReason.Refunded, CancellationToken.None));

        emails.Select(e => e.Recipient).Should().BeEquivalentTo(["admin@bookennis.com", "treasurer@test.com"]);
        emails.Should().OnlyContain(e => e.Type == ClubEmailType.CourtPurchaseNeedsAttention);
        var variables = emails[0].Variables.Should().BeOfType<CourtPurchaseNeedsAttentionEmailVariables>().Subject;
        variables.Buyer.Should().Be("First buyer");
        variables.Reason.Should().NotBeNullOrWhiteSpace().And.NotStartWith("CourtPurchase_Reason_", "the text comes from the resources");
        variables.Purchase.Url.Should().EndWith($"/clubs/{TestDataSeed.ClubId}/court-sales");
    }

    [Fact]
    public void PaidTexts_CoverEveryWayOfPaying()
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en");
        var slot = CourtSaleSeed.Tomorrow();

        CourtPurchase Make(int price, bool credit, int due, Action<CourtPurchase> act)
        {
            var purchase = new CourtPurchase(1, 1, 1, null, slot, new CourtQuote(price, "eur", []), credit, due);
            act(purchase);
            return purchase;
        }

        CourtPurchaseTexts.Paid(Make(2000, true, 0, p => p.CompleteWithoutPayment(DateTimeOffset.UtcNow)), culture).Should().Be("1 booking credit");
        CourtPurchaseTexts.Paid(Make(0, false, 0, p => p.CompleteWithoutPayment(DateTimeOffset.UtcNow)), culture).Should().Be("free");
        CourtPurchaseTexts.Paid(Make(2000, false, 2000, p => p.MarkPaid(DateTimeOffset.UtcNow, "pi")), culture).Should().Be("€20.00 online");
        CourtPurchaseTexts.Paid(Make(4000, true, 2000, p => p.MarkPaid(DateTimeOffset.UtcNow, "pi")), culture).Should().Be("1 booking credit + €20.00 online");
        CourtPurchaseTexts.Paid(Make(2000, false, 2000, _ => { }), culture).Should().Be("not paid yet");
    }

    private Task<int> Seed(bool creditUsed) => QueryAsync(async ctx =>
    {
        await CourtSaleSeed.EnableSales(ctx);
        var (_, buyer) = await CourtSaleSeed.AddBuyer(ctx);
        return (await CourtSaleSeed.AddPurchase(ctx, buyer, CourtSaleSeed.Tomorrow(), creditUsed: creditUsed)).Id;
    });

    private async Task<List<SendClubEmail>> Run(int purchaseId, Func<CourtPurchaseNotifier, CourtPurchase, Task> act)
    {
        var mediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var context = GetInstance<AppDbContext>();
            var purchase = await context.CourtPurchases.SingleAsync(p => p.Id == purchaseId);
            await act(new CourtPurchaseNotifier(context, mediator, GetInstance<AppSettings>()), purchase);
        });

        return mediator.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<SendClubEmail>().ToList();
    }
}
