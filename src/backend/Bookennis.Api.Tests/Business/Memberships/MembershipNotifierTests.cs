using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Memberships;

public class MembershipNotifierTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task PaymentReceived_GoesToThePayerWithTheDetailsOfTheOrder()
    {
        var (family, orderId) = await SeedFamilyOrder(order => order.MarkPaid(MembershipPaymentMethod.BankTransfer, DateTimeOffset.UtcNow));

        var emails = await Run(orderId, (notifier, order) => notifier.PaymentReceived(order, CancellationToken.None));

        var email = emails.Should().ContainSingle().Subject;
        email.Should().BeEquivalentTo(new { ClubId = TestDataSeed.ClubId, Type = ClubEmailType.MembershipPaymentReceived, Recipient = "user@bookennis.com", RecipientDisplayName = "us er", Language = Language.German }, o => o.ExcludingMissingMembers());

        var membership = email.Variables.Should().BeOfType<MembershipEmailVariables>().Subject.Membership;
        membership.Plan.Should().Be("Family");
        membership.Amount.Should().Be("€ 215,00");
        membership.Members.Should().Be("us er, Kid Tester, Partner Tester, Teen Tester");
        membership.PaymentMethod.Should().Be("Überweisung");
        membership.Season.Should().Contain(" - ");
        membership.Url.Should().Be($"http://notvalid.com/clubs/{TestDataSeed.ClubId}/membership?season={family.SeasonId}&order={orderId}");
    }

    [Fact]
    public async Task Approved_And_PaymentFailed_UseTheirOwnEmailTypes()
    {
        var (_, orderId) = await SeedFamilyOrder();

        var approved = await Run(orderId, (notifier, order) => notifier.Approved(order, CancellationToken.None));
        var failed = await Run(orderId, (notifier, order) => notifier.PaymentFailed(order, CancellationToken.None));

        approved.Should().ContainSingle().Which.Type.Should().Be(ClubEmailType.MembershipApproved);
        failed.Should().ContainSingle().Which.Type.Should().Be(ClubEmailType.MembershipPaymentFailed);
        ((MembershipEmailVariables)approved[0].Variables).Membership.PaymentMethod.Should().BeEmpty("nothing was paid yet");
    }

    [Fact]
    public async Task PaymentReceived_PayerIsAChildWithoutEmail_GoesToAParent()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            var family = await MembershipSeed.SeedFamily(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, family.SeasonId);
            return (await MembershipSeed.SeedOrder(ctx, plan, family.Kid, family.Kid)).Id;
        });

        var emails = await Run(orderId, (notifier, order) => notifier.PaymentReceived(order, CancellationToken.None));

        emails.Should().ContainSingle().Which.Recipient.Should().BeOneOf("user@bookennis.com", "partner@membership.test");
    }

    [Fact]
    public async Task PaymentReceived_PayerLeftTheClub_GoesToTheFirstMemberOfTheOrder()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id);
            return (await MembershipSeed.SeedOrder(ctx, plan, null, TestDataSeed.Member1Id)).Id;
        });

        var emails = await Run(orderId, (notifier, order) => notifier.PaymentReceived(order, CancellationToken.None));

        emails.Should().ContainSingle().Which.Recipient.Should().Be("user@bookennis.com");
    }

    [Fact]
    public async Task NeedsAttention_GoesToTreasurersAndAdminsInTheirLanguage()
    {
        var (family, orderId) = await SeedFamilyOrder();
        await QueryAsync(async ctx =>
        {
            await MembershipSeed.AddMember(ctx, "Treasurer", roles: [MemberRole.User, MemberRole.Treasurer], language: Language.English);
            await MembershipSeed.AddMember(ctx, "Trainer", roles: [MemberRole.User, MemberRole.Trainer]);
        });

        var emails = await Run(orderId, (notifier, order) => notifier.NeedsAttention(order, MembershipAttentionReason.Disputed, CancellationToken.None));

        emails.Select(e => e.Recipient).Should().BeEquivalentTo(["admin@bookennis.com", "treasurer@membership.test"]);
        emails.Should().OnlyContain(e => e.Type == ClubEmailType.MembershipNeedsAttention);

        var english = emails.Single(e => e.Recipient == "treasurer@membership.test").Variables.Should().BeOfType<MembershipNeedsAttentionEmailVariables>().Subject;
        english.Reason.Should().StartWith("The payer disputes the payment");
        english.Payer.Should().Be("us er");
        english.Membership.Amount.Should().Be("€215.00");
        english.Membership.Url.Should().Be($"http://notvalid.com/clubs/{TestDataSeed.ClubId}/membership-payments?season={family.SeasonId}");

        var german = (MembershipNeedsAttentionEmailVariables)emails.Single(e => e.Recipient == "admin@bookennis.com").Variables;
        german.Reason.Should().StartWith("Die Zahlung wurde bei der Bank beanstandet");
    }

    [Theory]
    [InlineData(MembershipAttentionReason.ApprovalRequested)]
    [InlineData(MembershipAttentionReason.PaymentFailed)]
    [InlineData(MembershipAttentionReason.Refunded)]
    [InlineData(MembershipAttentionReason.Disputed)]
    [InlineData(MembershipAttentionReason.UnexpectedPayment)]
    public async Task NeedsAttention_EveryReasonHasATextInBothLanguages(MembershipAttentionReason reason)
    {
        var (_, orderId) = await SeedFamilyOrder();
        await QueryAsync(ctx => MembershipSeed.AddMember(ctx, "Treasurer", roles: [MemberRole.Treasurer], language: Language.English));

        var emails = await Run(orderId, (notifier, order) => notifier.NeedsAttention(order, reason, CancellationToken.None));

        var reasons = emails.Select(e => ((MembershipNeedsAttentionEmailVariables)e.Variables).Reason).ToList();
        reasons.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        reasons.Should().OnlyContain(text => text.Length > 20 && !text.StartsWith("Membership_Reason_"));
    }

    [Theory]
    [InlineData(ClubEmailType.MembershipPaymentReceived)]
    [InlineData(ClubEmailType.MembershipApproved)]
    [InlineData(ClubEmailType.MembershipPaymentFailed)]
    [InlineData(ClubEmailType.MembershipNeedsAttention)]
    public async Task DefaultTemplates_RenderInBothLanguages(ClubEmailType type)
    {
        var renderer = GetInstance<IClubEmailRenderer>();
        var definition = ClubEmailCatalog.Get(type);

        foreach (var language in new[] { Language.German, Language.English })
        {
            var template = ClubEmailDefaultTemplates.Get(type, language);
            renderer.Validate(definition, template);

            var rendered = await renderer.Render(template, definition.CreateSample(language) with { Club = ClubEmailCatalog.SampleClub });

            rendered.Subject.Should().NotBeNullOrWhiteSpace().And.NotContain("{{");
            rendered.BodyHtml.Should().Contain("TC Example").And.Contain("215").And.Contain("https://www.example.com/clubs/1/membership").And.NotContain("{{");
        }
    }

    private Task<(SeededFamily Family, int OrderId)> SeedFamilyOrder(Action<MembershipOrder>? change = null) => QueryAsync(async ctx =>
    {
        var family = await MembershipSeed.SeedFamily(ctx);
        var plan = await MembershipSeed.SeedPlan(ctx, family.SeasonId, MembershipSeed.FamilyData());
        var order = await MembershipSeed.SeedOrder(ctx, plan, family.Parent, family.Everybody);
        change?.Invoke(order);
        await ctx.SaveChangesAsync();
        return (family, order.Id);
    });

    /// <summary>Runs the notifier with a mediator that only records the club emails (sending has no handler in tests).</summary>
    private async Task<List<SendClubEmail>> Run(int orderId, Func<MembershipNotifier, MembershipOrder, Task> act)
    {
        var mediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var context = GetInstance<AppDbContext>();
            var order = await context.MembershipOrders.Include(o => o.Members).SingleAsync(o => o.Id == orderId);
            await act(new MembershipNotifier(context, mediator, GetInstance<AppSettings>()), order);
        });

        return mediator.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<SendClubEmail>().ToList();
    }
}
