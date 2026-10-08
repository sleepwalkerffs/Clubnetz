using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.MemberBadges;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record UpdateMemberBadgeSettings(int MemberId, UpdateMemberBadgeSettingsModel Model) : ICommand
{
    public enum ErrorCode
    {
        CannotSetBothDisplayBadgeTypes = 0
    }

    public class Handler(AppDbContext context) : IRequestHandler<UpdateMemberBadgeSettings>
    {
        public async Task<Unit> Handle(UpdateMemberBadgeSettings request, CancellationToken cancellationToken)
        {
            if (request.Model.DisplayBadgeId.HasValue && request.Model.DisplayOneTimeBadgeId.HasValue)
                throw new PreconditionException(ErrorCode.CannotSetBothDisplayBadgeTypes, "Only one display badge can be selected at a time.");

            var settings = await context.MemberBadgeSettings
                .FirstOrDefaultAsync(s => s.MemberId == request.MemberId, cancellationToken);

            if (settings is null)
            {
                settings = new MemberBadgeSettings(request.MemberId);
                context.MemberBadgeSettings.Add(settings);
            }

            // Validate that the display badge belongs to this member
            if (request.Model.DisplayBadgeId.HasValue)
            {
                var badgeBelongsToMember = await context.MemberBadges
                    .AnyAsync(mb => mb.Id == request.Model.DisplayBadgeId.Value && mb.MemberId == request.MemberId, cancellationToken);

                settings.SetDisplayBadge(badgeBelongsToMember ? request.Model.DisplayBadgeId.Value : null);
            }
            else if (request.Model.DisplayOneTimeBadgeId.HasValue)
            {
                var badgeBelongsToMember = await context.MemberOneTimeBadges
                    .AnyAsync(motb => motb.Id == request.Model.DisplayOneTimeBadgeId.Value && motb.MemberId == request.MemberId, cancellationToken);

                settings.SetDisplayOneTimeBadge(badgeBelongsToMember ? request.Model.DisplayOneTimeBadgeId.Value : null);
            }
            else
            {
                settings.SetDisplayBadge(null);
                settings.SetDisplayOneTimeBadge(null);
            }

            settings.SetTrophyCaseVisibility(request.Model.TrophyCasePublic);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
