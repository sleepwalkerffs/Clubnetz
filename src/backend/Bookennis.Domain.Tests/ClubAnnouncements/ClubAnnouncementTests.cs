using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.ClubAnnouncements;

public class ClubAnnouncementTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(2));
    private static readonly DateOnly Today = new(2026, 10, 1);

    [Fact]
    public void Constructor_TrimsTextsAndStoresPublishedAtInUtc()
    {
        var announcement = new ClubAnnouncement(1, 5, new("  Summer party ", "  We **celebrate**.  ", true, Today.AddDays(7)), Now);

        announcement.ClubId.Should().Be(1);
        announcement.CreatedByMemberId.Should().Be(5);
        announcement.Title.Should().Be("Summer party");
        announcement.Body.Should().Be("We **celebrate**.");
        announcement.IsPinned.Should().BeTrue();
        announcement.ExpiresOn.Should().Be(Today.AddDays(7));
        announcement.PublishedAt.Should().Be(Now);
        announcement.PublishedAt.Offset.Should().Be(TimeSpan.Zero);
        announcement.EmailSentAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_EmptyTitle_Throws()
    {
        var act = () => new ClubAnnouncement(1, 1, Data() with { Title = "  " }, Now);

        ShouldThrow(act, ClubAnnouncement.ErrorCode.ClubAnnouncementTitleRequired);
    }

    [Fact]
    public void Constructor_EmptyBody_Throws()
    {
        var act = () => new ClubAnnouncement(1, 1, Data() with { Body = " \n " }, Now);

        ShouldThrow(act, ClubAnnouncement.ErrorCode.ClubAnnouncementBodyRequired);
    }

    [Fact]
    public void Update_TextTooLong_Throws()
    {
        var announcement = Create();

        var titleTooLong = () => announcement.Update(Data() with { Title = new string('a', ClubAnnouncement.MaxTitleLength + 1) });
        var bodyTooLong = () => announcement.Update(Data() with { Body = new string('a', ClubAnnouncement.MaxBodyLength + 1) });

        ShouldThrow(titleTooLong, ClubAnnouncement.ErrorCode.ClubAnnouncementTextTooLong);
        ShouldThrow(bodyTooLong, ClubAnnouncement.ErrorCode.ClubAnnouncementTextTooLong);
    }

    [Fact]
    public void Update_ReplacesDataAndKeepsPublishedAt()
    {
        var announcement = Create();

        announcement.Update(new("New title", "New text", true, Today));

        announcement.Title.Should().Be("New title");
        announcement.Body.Should().Be("New text");
        announcement.IsPinned.Should().BeTrue();
        announcement.ExpiresOn.Should().Be(Today);
        announcement.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void IsExpired_IsTrueAfterTheLastDay()
    {
        Create(expiresOn: null).IsExpired(Today).Should().BeFalse();
        Create(expiresOn: Today).IsExpired(Today).Should().BeFalse();
        Create(expiresOn: Today.AddDays(-1)).IsExpired(Today).Should().BeTrue();
    }

    [Fact]
    public void AddAttachment_StoresFileWithContentTypeOfTheExtension()
    {
        var announcement = Create();

        var attachment = announcement.AddAttachment("Flyer.PDF", [1, 2, 3]);

        announcement.Attachments.Should().ContainSingle().Which.Should().BeSameAs(attachment);
        attachment.FileName.Should().Be("Flyer.PDF");
        attachment.ContentType.Should().Be("application/pdf");
        attachment.Size.Should().Be(3);
        attachment.Content.Data.Should().Equal(1, 2, 3);
    }

    [Theory]
    [InlineData(@"C:\Users\me\Documents\plan.pdf", "plan.pdf")]
    [InlineData("../../etc/plan.pdf", "plan.pdf")]
    [InlineData("  my \"plan\"\r\n.pdf", "my plan.pdf")]
    [InlineData(".pdf", "attachment.pdf")]
    public void AddAttachment_NormalizesTheFileName(string fileName, string expected)
    {
        var attachment = Create().AddAttachment(fileName, [1]);

        attachment.FileName.Should().Be(expected);
    }

    [Fact]
    public void AddAttachment_LongFileName_IsShortenedButKeepsTheExtension()
    {
        var attachment = Create().AddAttachment(new string('a', 300) + ".docx", [1]);

        attachment.FileName.Should().HaveLength(ClubAnnouncement.MaxFileNameLength).And.EndWith(".docx");
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("page.html")]
    [InlineData("image.svg")]
    [InlineData("noextension")]
    public void AddAttachment_FileTypeNotAllowed_Throws(string fileName)
    {
        var act = () => Create().AddAttachment(fileName, [1]);

        ShouldThrow(act, ClubAnnouncement.ErrorCode.ClubAnnouncementAttachmentTypeNotAllowed);
    }

    [Fact]
    public void AddAttachment_EmptyFile_Throws()
    {
        var act = () => Create().AddAttachment("empty.pdf", []);

        ShouldThrow(act, ClubAnnouncement.ErrorCode.ClubAnnouncementAttachmentEmpty);
    }

    [Fact]
    public void AddAttachment_FileTooLarge_Throws()
    {
        var act = () => Create().AddAttachment("large.pdf", new byte[ClubAnnouncement.MaxAttachmentSizeInBytes + 1]);

        ShouldThrow(act, ClubAnnouncement.ErrorCode.ClubAnnouncementAttachmentTooLarge)
            .Which.ErrorDetails.Should().Equal("5");
    }

    [Fact]
    public void AddAttachment_TooManyAttachments_Throws()
    {
        var announcement = Create();
        for (var i = 0; i < ClubAnnouncement.MaxAttachments; i++)
            announcement.AddAttachment($"file{i}.pdf", [1]);

        var act = () => announcement.AddAttachment("one-too-many.pdf", [1]);

        ShouldThrow(act, ClubAnnouncement.ErrorCode.ClubAnnouncementTooManyAttachments);
    }

    [Fact]
    public void AddAttachment_TotalSizeTooLarge_Throws()
    {
        var announcement = Create();
        announcement.AddAttachment("a.pdf", new byte[ClubAnnouncement.MaxAttachmentSizeInBytes]);
        announcement.AddAttachment("b.pdf", new byte[ClubAnnouncement.MaxAttachmentSizeInBytes]);

        var act = () => announcement.AddAttachment("c.pdf", [1]);

        ShouldThrow(act, ClubAnnouncement.ErrorCode.ClubAnnouncementAttachmentsTooLarge)
            .Which.ErrorDetails.Should().Equal("10");
    }

    [Fact]
    public void RemoveAttachment_RemovesOnlyTheGivenAttachment()
    {
        var announcement = Create();
        announcement.AddAttachment("a.pdf", [1]).SetId(11);
        announcement.AddAttachment("b.pdf", [1]).SetId(12);

        announcement.RemoveAttachment(11).Should().BeTrue();
        announcement.RemoveAttachment(99).Should().BeFalse();

        announcement.Attachments.Should().ContainSingle().Which.FileName.Should().Be("b.pdf");
    }

    [Fact]
    public void MarkEmailSent_StoresAudienceAndRecipientCount()
    {
        var announcement = Create();

        announcement.MarkEmailSent(ClubAnnouncementAudience.Youth, 42, Now.AddHours(1));

        announcement.EmailSentAt.Should().Be(Now.AddHours(1));
        announcement.EmailAudience.Should().Be(ClubAnnouncementAudience.Youth);
        announcement.EmailRecipientCount.Should().Be(42);
    }

    private static ClubAnnouncement.AnnouncementData Data() => new("Summer party", "We celebrate.", false, null);

    private static ClubAnnouncement Create(DateOnly? expiresOn = null) => new(1, 1, Data() with { ExpiresOn = expiresOn }, Now);

    private static FluentAssertions.Specialized.ExceptionAssertions<PreconditionException> ShouldThrow<T>(Func<T> act, ClubAnnouncement.ErrorCode errorCode)
    {
        var assertion = act.Should().Throw<PreconditionException>();
        assertion.Which.ErrorCode.Should().Be(errorCode.ToString());
        return assertion;
    }

    private static void ShouldThrow(Action act, ClubAnnouncement.ErrorCode errorCode)
        => act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(errorCode.ToString());
}
