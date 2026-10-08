using Bookennis.Api.Data;
using Bookennis.Domain.ClubAnnouncements;
using Microsoft.AspNetCore.Http;

namespace Bookennis.Api.Tests.Business.ClubAnnouncements;

internal static class ClubAnnouncementSeed
{
    public static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public static ClubAnnouncement.AnnouncementData Data(string title = "Summer party", bool isPinned = false, DateOnly? expiresOn = null)
        => new(title, "We **celebrate** on Saturday.\n\nSee you [there](https://example.com/party)!", isPinned, expiresOn);

    public static async Task<ClubAnnouncement> Seed(AppDbContext context, ClubAnnouncement.AnnouncementData? data = null, DateTimeOffset? publishedAt = null)
    {
        var announcement = new ClubAnnouncement(TestDataSeed.ClubId, TestDataSeed.Member2Id, data ?? Data(), publishedAt ?? DateTimeOffset.UtcNow);
        context.ClubAnnouncements.Add(announcement);
        await context.SaveChangesAsync();
        return announcement;
    }

    /// <summary>An announcement with the attachments "flyer.pdf" (1, 2, 3) and "plan.xlsx" (4, 5).</summary>
    public static async Task<ClubAnnouncement> SeedWithAttachments(AppDbContext context, ClubAnnouncement.AnnouncementData? data = null)
    {
        var announcement = await Seed(context, data);
        announcement.AddAttachment("flyer.pdf", [1, 2, 3]);
        await context.SaveChangesAsync();
        announcement.AddAttachment("plan.xlsx", [4, 5]);
        await context.SaveChangesAsync();
        return announcement;
    }

    public static IFormFile File(string fileName, byte[] content)
        => new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName);
}
