using System.Net.Mail;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubEmails;

public record UpdateClubEmailSettings(int ClubId, string? WebsiteUrl, string? ReplyToEmail) : ICommand
{
    public enum ErrorCode
    {
        InvalidWebsiteUrl = 0,
        InvalidReplyToEmail = 1
    }

    public class Handler(AppDbContext context) : IRequestHandler<UpdateClubEmailSettings>
    {
        public async Task<Unit> Handle(UpdateClubEmailSettings request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.WebsiteUrl) && !Club.IsValidWebsiteUrl(request.WebsiteUrl.Trim()))
                throw new PreconditionException(ErrorCode.InvalidWebsiteUrl, "The website url must be an absolute http(s) url.");
            if (!string.IsNullOrWhiteSpace(request.ReplyToEmail) && !MailAddress.TryCreate(request.ReplyToEmail.Trim(), out _))
                throw new PreconditionException(ErrorCode.InvalidReplyToEmail, "The reply-to email is not a valid email address.");

            var club = await context.Clubs.SingleRequiredAsync(c => c.Id == request.ClubId, cancellationToken);
            club.UpdateContactSettings(request.WebsiteUrl, request.ReplyToEmail);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
