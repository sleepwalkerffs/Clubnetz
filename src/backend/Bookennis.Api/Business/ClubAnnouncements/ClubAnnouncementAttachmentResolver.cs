using System.Globalization;
using Bookennis.Api.Data;
using Bookennis.Domain.ClubAnnouncements;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.Email;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubAnnouncements;

/// <summary>
/// Loads the attachments of announcement emails from the database when the email is sent.
/// The email job only carries a reference (<c>club-announcement-attachment:{id}</c>), not the file.
/// </summary>
public class ClubAnnouncementAttachmentResolver(AppDbContext context) : IEmailAttachmentResolver
{
    public const string Scheme = "club-announcement-attachment";

    public static Uri CreateUri(int attachmentId) => new($"{Scheme}:{attachmentId.ToString(CultureInfo.InvariantCulture)}");

    public bool Supports(Uri uri) => uri.Scheme == Scheme;

    public async Task<Stream> GetAttachmentAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!int.TryParse(uri.AbsolutePath, NumberStyles.None, CultureInfo.InvariantCulture, out var attachmentId))
            throw new ArgumentException($"'{uri}' is not a valid attachment reference.", nameof(uri));

        var data = await context.Set<ClubAnnouncementAttachmentContent>()
            .Where(c => c.ClubAnnouncementAttachmentId == attachmentId)
            .Select(c => c.Data)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException(typeof(ClubAnnouncementAttachment), attachmentId);

        return new MemoryStream(data, writable: false);
    }
}
