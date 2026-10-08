using Bookennis.Api.Business.ClubAnnouncements;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubAnnouncements;

public class ClubAnnouncementCrudTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreateClubAnnouncement_StoresAnnouncementWithAuthor()
    {
        var id = await SendAsync(new CreateClubAnnouncement(TestDataSeed.ClubId, TestDataSeed.AdminId, ClubAnnouncementSeed.Data(isPinned: true, expiresOn: ClubAnnouncementSeed.Today.AddDays(5))));

        var announcement = await QueryAsync(ctx => ctx.ClubAnnouncements.SingleAsync(a => a.Id == id));

        announcement.ClubId.Should().Be(TestDataSeed.ClubId);
        announcement.CreatedByMemberId.Should().Be(TestDataSeed.Member2Id);
        announcement.Title.Should().Be("Summer party");
        announcement.IsPinned.Should().BeTrue();
        announcement.ExpiresOn.Should().Be(ClubAnnouncementSeed.Today.AddDays(5));
        announcement.PublishedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateClubAnnouncement_UserWithoutMembership_StoresAnnouncementWithoutAuthor()
    {
        var id = await SendAsync(new CreateClubAnnouncement(TestDataSeed.ClubId, 999_999, ClubAnnouncementSeed.Data()));

        var announcement = await QueryAsync(ctx => ctx.ClubAnnouncements.SingleAsync(a => a.Id == id));
        announcement.CreatedByMemberId.Should().BeNull();
    }

    [Fact]
    public async Task CreateClubAnnouncement_EmptyBody_ThrowsPreconditionException()
    {
        var act = () => SendAsync(new CreateClubAnnouncement(TestDataSeed.ClubId, TestDataSeed.AdminId, ClubAnnouncementSeed.Data() with { Body = "" }));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(ClubAnnouncement.ErrorCode.ClubAnnouncementBodyRequired));
    }

    [Fact]
    public async Task UpdateClubAnnouncement_ReplacesDataAndKeepsAttachments()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.SeedWithAttachments(ctx)).Id);

        var result = await SendAsync(new UpdateClubAnnouncement(TestDataSeed.ClubId, id, new("Autumn party", "Now in *autumn*.", true, ClubAnnouncementSeed.Today)));

        result.Title.Should().Be("Autumn party");
        result.Body.Should().Be("Now in *autumn*.");
        result.BodyHtml.Should().Contain("<em>autumn</em>");
        result.IsPinned.Should().BeTrue();
        result.ExpiresOn.Should().Be(ClubAnnouncementSeed.Today);
        result.Attachments.Select(a => a.FileName).Should().Equal("flyer.pdf", "plan.xlsx");

        var stored = await QueryAsync(ctx => ctx.ClubAnnouncements.SingleAsync(a => a.Id == id));
        stored.Title.Should().Be("Autumn party");
    }

    [Fact]
    public async Task UpdateClubAnnouncement_OfAnotherClub_ThrowsNotFound()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.Seed(ctx)).Id);

        var act = () => SendAsync(new UpdateClubAnnouncement(TestDataSeed.ClubId + 1, id, ClubAnnouncementSeed.Data()));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task DeleteClubAnnouncement_RemovesAnnouncementWithAttachmentsAndFiles()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.SeedWithAttachments(ctx)).Id);

        await SendAsync(new DeleteClubAnnouncement(TestDataSeed.ClubId, id));

        var (announcements, attachments, contents) = await QueryAsync(async ctx => (
            await ctx.ClubAnnouncements.CountAsync(),
            await ctx.ClubAnnouncementAttachments.CountAsync(),
            await ctx.Set<ClubAnnouncementAttachmentContent>().CountAsync()));

        announcements.Should().Be(0);
        attachments.Should().Be(0);
        contents.Should().Be(0);
    }

    [Fact]
    public async Task DeleteClubAnnouncement_Unknown_ThrowsNotFound()
    {
        var act = () => SendAsync(new DeleteClubAnnouncement(TestDataSeed.ClubId, 999_999));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task PreviewClubAnnouncement_RendersSanitizedMarkdown()
    {
        var result = await SendAsync(new PreviewClubAnnouncement("**Bold** <script>alert(1)</script>\n\n[Link](https://example.com) ![Image](https://example.com/a.png) [Bad](javascript:alert(1))"));

        result.Html.Should().Contain("<strong>Bold</strong>");
        result.Html.Should().Contain("href=\"https://example.com\"").And.Contain("target=\"_blank\"").And.Contain("rel=\"noopener noreferrer\"");
        result.Html.Should().NotContain("<script").And.NotContain("<img").And.NotContain("javascript:");
    }

    [Fact]
    public async Task PreviewClubAnnouncement_TextTooLong_ThrowsPreconditionException()
    {
        var act = () => SendAsync(new PreviewClubAnnouncement(new string('a', ClubAnnouncement.MaxBodyLength + 1)));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(ClubAnnouncement.ErrorCode.ClubAnnouncementTextTooLong));
    }
}
