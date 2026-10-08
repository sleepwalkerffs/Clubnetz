using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.ClubAnnouncements;

namespace Bookennis.Api.Business.ClubAnnouncements;

/// <summary>Attaches a file to an announcement. File type, size and number of attachments are validated by the domain.</summary>
public record AddClubAnnouncementAttachment(int ClubId, int ClubAnnouncementId, IFormFile File) : ICommand<ClubAnnouncementDto>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<AddClubAnnouncementAttachment, ClubAnnouncementDto>
    {
        public async Task<ClubAnnouncementDto> Handle(AddClubAnnouncementAttachment request, CancellationToken cancellationToken)
        {
            var announcement = await context.ClubAnnouncements.GetAnnouncementWithAttachments(request.ClubId, request.ClubAnnouncementId, cancellationToken);

            // Checked before reading, so that an oversized upload is not loaded into memory
            if (request.File.Length > ClubAnnouncement.MaxAttachmentSizeInBytes)
            {
                throw new PreconditionException(
                    ClubAnnouncement.ErrorCode.ClubAnnouncementAttachmentTooLarge,
                    [(ClubAnnouncement.MaxAttachmentSizeInBytes / (1024 * 1024)).ToString()],
                    "The file is too large.");
            }

            await using var input = request.File.OpenReadStream();
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, cancellationToken);

            announcement.AddAttachment(request.File.FileName, buffer.ToArray());

            await context.SaveChangesAsync(cancellationToken);

            return await ClubAnnouncementMapper.ToDto(context, renderer, announcement, canManage: true, cancellationToken);
        }
    }
}
