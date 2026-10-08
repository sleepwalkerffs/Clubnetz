using Bookennis.Api.Data;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubAnnouncements;
using ClubAnnouncementAudience = Bookennis.Domain.ClubAnnouncements.ClubAnnouncementAudience;

namespace Bookennis.Api.Business.ClubAnnouncements;

/// <summary>How many emails an audience results in, shown before an announcement is sent.</summary>
public record GetClubAnnouncementEmailRecipients(int ClubId, ClubAnnouncementAudience Audience, IReadOnlyCollection<MemberRole> Roles) : IQuery<ClubAnnouncementEmailRecipientsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubAnnouncementEmailRecipients, ClubAnnouncementEmailRecipientsResult>
    {
        public async Task<ClubAnnouncementEmailRecipientsResult> Handle(GetClubAnnouncementEmailRecipients request, CancellationToken cancellationToken)
        {
            var recipients = await ClubAnnouncementRecipients.Resolve(context, request.ClubId, request.Audience, request.Roles, cancellationToken);
            return new ClubAnnouncementEmailRecipientsResult { RecipientCount = recipients.Count };
        }
    }
}
