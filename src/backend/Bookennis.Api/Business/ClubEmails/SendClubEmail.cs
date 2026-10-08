using Bookennis.Api.Business.ClubEmails.EmailViewModels;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Email;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubEmails;

/// <summary>
/// Sends an email that is triggered by something happening in a club, using the club's template for
/// <paramref name="Type"/> (or the built-in default). <paramref name="TemplateOverride"/> is used to send
/// unsaved templates (test emails). <paramref name="Attachments"/> are loaded by the registered
/// <see cref="IEmailAttachmentResolver"/>s when the email is sent.
/// </summary>
public record SendClubEmail(
    int ClubId,
    ClubEmailType Type,
    string Recipient,
    string RecipientDisplayName,
    Language Language,
    ClubEmailVariables Variables,
    ClubEmailTemplateContent? TemplateOverride = null,
    IReadOnlyList<Attachment>? Attachments = null) : ICommand
{
    public class Handler(
        AppDbContext context,
        IMediator mediator,
        IClubEmailRenderer renderer,
        AppSettings appSettings,
        ILogger<SendClubEmail> logger) : IRequestHandler<SendClubEmail>
    {
        public async Task<Unit> Handle(SendClubEmail request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs
                .IgnoreQueryFilters()
                .Where(c => c.Id == request.ClubId)
                .Select(c => new ClubVariables(c.Name, c.WebsiteUrl, c.ReplyToEmail))
                .SingleOrDefaultAsync(cancellationToken);

            if (club is null)
                return default;

            var variables = request.Variables with { Club = club };
            var template = request.TemplateOverride
                ?? await ClubEmailTemplateResolver.Resolve(context, request.ClubId, request.Type, request.Language, cancellationToken);

            RenderedClubEmail rendered;
            try
            {
                rendered = await renderer.Render(template, variables);
            }
            catch (PreconditionException e) when (request.TemplateOverride is null)
            {
                // A saved template should always render (it is validated on save). Never lose the email because of it.
                logger.LogWarning(e, "Club email template {Type} of club {ClubId} could not be rendered, falling back to the default template.", request.Type, request.ClubId);
                rendered = await renderer.Render(ClubEmailDefaultTemplates.Get(request.Type, request.Language), variables);
            }

            await mediator.Send(
                new SendEmail(
                    request.Recipient.ToLowerInvariant(),
                    request.RecipientDisplayName,
                    request.Language.ToCultureInfo(),
                    new ClubEmailViewModel(rendered.Subject, rendered.BodyHtml),
                    // Fusonic uses the subject key as subject if there is no resource for it, and runs it through string.Format.
                    SubjectKey: rendered.Subject.Replace("{", "{{").Replace("}", "}}"),
                    BccRecipient: appSettings.BccRecipient,
                    Attachments: request.Attachments is { Count: > 0 } ? [.. request.Attachments] : null,
                    ReplyTo: club.ReplyToEmail),
                cancellationToken);

            return default;
        }
    }
}
