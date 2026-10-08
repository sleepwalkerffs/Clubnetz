using Bookennis.Api.Business.ClubAnnouncements;
using Bookennis.Api.Data;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubAnnouncements;

public class ClubAnnouncementAttachmentTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AddClubAnnouncementAttachment_StoresTheFile()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.Seed(ctx)).Id);

        var result = await SendAsync(new AddClubAnnouncementAttachment(TestDataSeed.ClubId, id, ClubAnnouncementSeed.File("Flyer.pdf", [7, 8, 9])));

        var attachment = result.Attachments.Should().ContainSingle().Subject;
        attachment.FileName.Should().Be("Flyer.pdf");
        attachment.ContentType.Should().Be("application/pdf");
        attachment.Size.Should().Be(3);

        var data = await QueryAsync(ctx => ctx.Set<ClubAnnouncementAttachmentContent>().Where(c => c.ClubAnnouncementAttachmentId == attachment.Id).Select(c => c.Data).SingleAsync());
        data.Should().Equal(7, 8, 9);
    }

    [Fact]
    public async Task AddClubAnnouncementAttachment_SecondFile_KeepsTheFirst()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.SeedWithAttachments(ctx)).Id);

        var result = await SendAsync(new AddClubAnnouncementAttachment(TestDataSeed.ClubId, id, ClubAnnouncementSeed.File("photo.jpg", [1])));

        result.Attachments.Select(a => a.FileName).Should().Equal("flyer.pdf", "plan.xlsx", "photo.jpg");
    }

    [Fact]
    public async Task AddClubAnnouncementAttachment_FileTypeNotAllowed_ThrowsPreconditionException()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.Seed(ctx)).Id);

        var act = () => SendAsync(new AddClubAnnouncementAttachment(TestDataSeed.ClubId, id, ClubAnnouncementSeed.File("setup.exe", [1])));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(ClubAnnouncement.ErrorCode.ClubAnnouncementAttachmentTypeNotAllowed));
    }

    [Fact]
    public async Task AddClubAnnouncementAttachment_FileTooLarge_ThrowsPreconditionException()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.Seed(ctx)).Id);

        var act = () => SendAsync(new AddClubAnnouncementAttachment(TestDataSeed.ClubId, id,
            ClubAnnouncementSeed.File("large.pdf", new byte[ClubAnnouncement.MaxAttachmentSizeInBytes + 1])));

        var exception = (await act.Should().ThrowAsync<PreconditionException>()).Which;
        exception.ErrorCode.Should().Be(nameof(ClubAnnouncement.ErrorCode.ClubAnnouncementAttachmentTooLarge));
        exception.ErrorDetails.Should().Equal("5");
    }

    [Fact]
    public async Task AddClubAnnouncementAttachment_OfAnotherClub_ThrowsNotFound()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.Seed(ctx)).Id);

        var act = () => SendAsync(new AddClubAnnouncementAttachment(TestDataSeed.ClubId + 1, id, ClubAnnouncementSeed.File("flyer.pdf", [1])));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task RemoveClubAnnouncementAttachment_RemovesAttachmentAndFile()
    {
        var (id, flyerId) = await QueryAsync(async ctx =>
        {
            var announcement = await ClubAnnouncementSeed.SeedWithAttachments(ctx);
            return (announcement.Id, announcement.Attachments.Single(a => a.FileName == "flyer.pdf").Id);
        });

        var result = await SendAsync(new RemoveClubAnnouncementAttachment(TestDataSeed.ClubId, id, flyerId));

        result.Attachments.Select(a => a.FileName).Should().Equal("plan.xlsx");

        var contentIds = await QueryAsync(ctx => ctx.Set<ClubAnnouncementAttachmentContent>().Select(c => c.ClubAnnouncementAttachmentId).ToListAsync());
        contentIds.Should().ContainSingle().Which.Should().NotBe(flyerId);
    }

    [Fact]
    public async Task RemoveClubAnnouncementAttachment_UnknownAttachment_ChangesNothing()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.SeedWithAttachments(ctx)).Id);

        var result = await SendAsync(new RemoveClubAnnouncementAttachment(TestDataSeed.ClubId, id, 999_999));

        result.Attachments.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetClubAnnouncementAttachment_ReturnsTheFile()
    {
        var (id, planId) = await SeedAnnouncementWithPlan();

        var result = await SendAsync(new GetClubAnnouncementAttachment(TestDataSeed.ClubId, id, planId, CanManage: false));

        result.FileName.Should().Be("plan.xlsx");
        result.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.Data.Should().Equal(4, 5);
    }

    [Fact]
    public async Task GetClubAnnouncementAttachment_OfExpiredAnnouncement_IsOnlyAvailableToManagers()
    {
        var (id, planId) = await SeedAnnouncementWithPlan(ClubAnnouncementSeed.Data(expiresOn: ClubAnnouncementSeed.Today.AddDays(-1)));

        var forMember = () => SendAsync(new GetClubAnnouncementAttachment(TestDataSeed.ClubId, id, planId, CanManage: false));
        var forManager = await SendAsync(new GetClubAnnouncementAttachment(TestDataSeed.ClubId, id, planId, CanManage: true));

        await forMember.Should().ThrowAsync<EntityNotFoundException>();
        forManager.Data.Should().Equal(4, 5);
    }

    [Fact]
    public async Task GetClubAnnouncementAttachment_WrongAnnouncementOrClub_ThrowsNotFound()
    {
        var (id, planId) = await SeedAnnouncementWithPlan();

        var wrongAnnouncement = () => SendAsync(new GetClubAnnouncementAttachment(TestDataSeed.ClubId, id + 1, planId, CanManage: true));
        var wrongClub = () => SendAsync(new GetClubAnnouncementAttachment(TestDataSeed.ClubId + 1, id, planId, CanManage: true));

        await wrongAnnouncement.Should().ThrowAsync<EntityNotFoundException>();
        await wrongClub.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task AttachmentResolver_LoadsTheFileOfItsUri()
    {
        var (_, planId) = await SeedAnnouncementWithPlan();
        var uri = ClubAnnouncementAttachmentResolver.CreateUri(planId);

        var (supportsOwn, supportsFile, data) = await ScopedAsync(async () =>
        {
            var resolver = new ClubAnnouncementAttachmentResolver(GetInstance<AppDbContext>());
            await using var stream = await resolver.GetAttachmentAsync(uri, CancellationToken.None);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return (resolver.Supports(uri), resolver.Supports(new Uri("file:///tmp/a.pdf")), buffer.ToArray());
        });

        uri.ToString().Should().Be($"club-announcement-attachment:{planId}");
        supportsOwn.Should().BeTrue();
        supportsFile.Should().BeFalse();
        data.Should().Equal(4, 5);
    }

    [Fact]
    public async Task AttachmentResolver_DeletedAttachment_ThrowsNotFound()
    {
        var act = () => ScopedAsync(() => new ClubAnnouncementAttachmentResolver(GetInstance<AppDbContext>())
            .GetAttachmentAsync(ClubAnnouncementAttachmentResolver.CreateUri(999_999), CancellationToken.None));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    private Task<(int AnnouncementId, int PlanId)> SeedAnnouncementWithPlan(ClubAnnouncement.AnnouncementData? data = null)
        => QueryAsync(async ctx =>
        {
            var announcement = await ClubAnnouncementSeed.SeedWithAttachments(ctx, data);
            return (announcement.Id, announcement.Attachments.Single(a => a.FileName == "plan.xlsx").Id);
        });
}
