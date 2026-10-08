using Bookennis.Api.Business.ClubAnnouncements;
using Bookennis.Domain.ClubAnnouncements;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Xunit;
using SharedAudience = Bookennis.Shared.Controller.ClubAnnouncements.ClubAnnouncementAudience;

namespace Bookennis.Api.Tests.Business.ClubAnnouncements;

public class GetClubAnnouncementsTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly DateOnly Today = ClubAnnouncementSeed.Today;

    [Fact]
    public async Task GetClubAnnouncements_ReturnsPinnedFirstThenNewestWithoutExpired()
    {
        var (older, newer, pinned) = await SeedAnnouncements();

        var result = await SendAsync(new GetClubAnnouncements(TestDataSeed.ClubId, CanManage: false));

        result.Announcements.Select(a => a.Id).Should().Equal(pinned, newer, older);

        var summary = result.Announcements.Single(a => a.Id == newer);
        summary.Title.Should().Be("Newer");
        summary.Excerpt.Should().Be("We celebrate on Saturday. See you there!");
        summary.IsPinned.Should().BeFalse();
        summary.IsExpired.Should().BeFalse();
        summary.AttachmentCount.Should().Be(2);
        summary.CreatedByName.Should().Be("ad min");
        summary.ExpiresOn.Should().Be(Today);
    }

    [Fact]
    public async Task GetClubAnnouncements_IncludeExpired_IsOnlyRespectedForManagers()
    {
        var (older, newer, pinned) = await SeedAnnouncements();

        var forMember = await SendAsync(new GetClubAnnouncements(TestDataSeed.ClubId, CanManage: false, IncludeExpired: true));
        var forManager = await SendAsync(new GetClubAnnouncements(TestDataSeed.ClubId, CanManage: true, IncludeExpired: true));

        forMember.Announcements.Should().HaveCount(3);

        forManager.Announcements.Should().HaveCount(4);
        forManager.Announcements.Take(3).Select(a => a.Id).Should().Equal(pinned, newer, older);
        var expired = forManager.Announcements.Last();
        expired.Title.Should().Be("Expired");
        expired.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task GetClubAnnouncements_Take_LimitsTheResult()
    {
        var (_, newer, pinned) = await SeedAnnouncements();

        var result = await SendAsync(new GetClubAnnouncements(TestDataSeed.ClubId, CanManage: false, Take: 2));

        result.Announcements.Select(a => a.Id).Should().Equal(pinned, newer);
    }

    [Fact]
    public async Task GetClubAnnouncements_LongText_IsShortenedToAnExcerpt()
    {
        await QueryAsync(ctx => ClubAnnouncementSeed.Seed(ctx, ClubAnnouncementSeed.Data() with { Body = string.Join(' ', Enumerable.Repeat("word", 200)) }));

        var result = await SendAsync(new GetClubAnnouncements(TestDataSeed.ClubId, CanManage: false));

        var excerpt = result.Announcements.Single().Excerpt;
        excerpt.Should().EndWith("…");
        excerpt.Length.Should().BeLessThanOrEqualTo(ClubAnnouncementMapper.ExcerptLength + 1);
    }

    [Fact]
    public async Task GetClubAnnouncement_ReturnsRenderedBodyAndAttachments()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.SeedWithAttachments(ctx)).Id);

        var result = await SendAsync(new GetClubAnnouncement(TestDataSeed.ClubId, id, CanManage: false));

        result.Title.Should().Be("Summer party");
        result.Body.Should().StartWith("We **celebrate**");
        result.BodyHtml.Should().Contain("<strong>celebrate</strong>").And.Contain("href=\"https://example.com/party\"");
        result.CreatedByName.Should().Be("ad min");
        result.IsExpired.Should().BeFalse();
        result.Attachments.Select(a => (a.FileName, a.ContentType, a.Size)).Should().Equal(
            ("flyer.pdf", "application/pdf", 3),
            ("plan.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 2));
    }

    [Fact]
    public async Task GetClubAnnouncement_EmailDetails_AreOnlyReturnedToManagers()
    {
        var id = await QueryAsync(async ctx =>
        {
            var announcement = await ClubAnnouncementSeed.Seed(ctx);
            announcement.MarkEmailSent(ClubAnnouncementAudience.Youth, 12, Now);
            await ctx.SaveChangesAsync();
            return announcement.Id;
        });

        var forMember = await SendAsync(new GetClubAnnouncement(TestDataSeed.ClubId, id, CanManage: false));
        var forManager = await SendAsync(new GetClubAnnouncement(TestDataSeed.ClubId, id, CanManage: true));

        forMember.EmailSentAt.Should().BeNull();
        forMember.EmailAudience.Should().BeNull();
        forMember.EmailRecipientCount.Should().BeNull();

        forManager.EmailSentAt.Should().BeCloseTo(Now, TimeSpan.FromSeconds(1));
        forManager.EmailAudience.Should().Be(SharedAudience.Youth);
        forManager.EmailRecipientCount.Should().Be(12);
    }

    [Fact]
    public async Task GetClubAnnouncement_Expired_IsOnlyVisibleToManagers()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.Seed(ctx, ClubAnnouncementSeed.Data(expiresOn: Today.AddDays(-1)))).Id);

        var forMember = () => SendAsync(new GetClubAnnouncement(TestDataSeed.ClubId, id, CanManage: false));
        var forManager = await SendAsync(new GetClubAnnouncement(TestDataSeed.ClubId, id, CanManage: true));

        await forMember.Should().ThrowAsync<EntityNotFoundException>();
        forManager.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task GetClubAnnouncement_OfAnotherClub_ThrowsNotFound()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.Seed(ctx)).Id);

        var act = () => SendAsync(new GetClubAnnouncement(TestDataSeed.ClubId + 1, id, CanManage: true));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    /// <summary>Seeds an older, a newer (with attachments, expiring today), a pinned (oldest) and an expired pinned announcement.</summary>
    private Task<(int Older, int Newer, int Pinned)> SeedAnnouncements()
        => QueryAsync(async ctx =>
        {
            var pinned = await ClubAnnouncementSeed.Seed(ctx, ClubAnnouncementSeed.Data("Pinned", isPinned: true), Now.AddDays(-30));
            var older = await ClubAnnouncementSeed.Seed(ctx, ClubAnnouncementSeed.Data("Older"), Now.AddDays(-2));
            var newer = await ClubAnnouncementSeed.SeedWithAttachments(ctx, ClubAnnouncementSeed.Data("Newer", expiresOn: Today));
            await ClubAnnouncementSeed.Seed(ctx, ClubAnnouncementSeed.Data("Expired", isPinned: true, expiresOn: Today.AddDays(-1)), Now.AddDays(-1));
            return (older.Id, newer.Id, pinned.Id);
        });
}
