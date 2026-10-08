using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubEmails;

/// <summary>Sends a (not yet saved) template with sample data to the current user.</summary>
public record SendTestClubEmail(int ClubId, ClubEmailType Type, Language Language, string Subject, string Body) : ICommand
{
    public enum ErrorCode
    {
        CurrentUserHasNoEmail = 0
    }

    public class Handler(AppDbContext context, IClubEmailRenderer renderer, IUserAccessor userAccessor, IMediator mediator) : IRequestHandler<SendTestClubEmail>
    {
        public async Task<Unit> Handle(SendTestClubEmail request, CancellationToken cancellationToken)
        {
            var definition = ClubEmailCatalog.Get(request.Type);
            var template = new ClubEmailTemplateContent(request.Subject, request.Body);
            renderer.Validate(definition, template);

            var user = await context.Users.FindRequiredAsync(userAccessor.GetUserId(), cancellationToken);
            if (string.IsNullOrWhiteSpace(user.Email))
                throw new PreconditionException(ErrorCode.CurrentUserHasNoEmail, "The current user has no email address.");

            await mediator.Send(
                new SendClubEmail(
                    request.ClubId,
                    request.Type,
                    user.Email,
                    user.FullName,
                    request.Language,
                    definition.CreateSample(request.Language),
                    template),
                cancellationToken);

            return default;
        }
    }
}
