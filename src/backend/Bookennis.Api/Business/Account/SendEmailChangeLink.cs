using Bookennis.Api.Business.Admin;
using Bookennis.Api.Config;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.User;
using Fusonic.Extensions.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Texts = Bookennis.Api.Resources.Localization.View_Emails_ConfirmEmailChange;

namespace Bookennis.Api.Business.Account;

/// <summary>
/// Sends the confirmation link for an email change to the new address. The email is only changed once the link is confirmed (see <see cref="ConfirmEmailChange"/>).
/// </summary>
public record SendEmailChangeLink(int UserId, string NewEmail, bool RequestedByAdmin) : ICommand
{
    public class Handler(UserManager<User> userManager, IMediator mediator, AppSettings appSettings) : IRequestHandler<SendEmailChangeLink>
    {
        public async Task<Unit> Handle(SendEmailChangeLink request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.UserId.ToString())
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(User), request.UserId);

            var token = await userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);

            var culture = user.Language.ToCultureInfo();
            var url = appSettings.AppUri.AbsoluteUri.TrimEnd('/') + "/Account/ConfirmEmailChange";
            var confirmUrl = new Uri(QueryHelpers.AddQueryString(url, new Dictionary<string, string?>
            {
                { "userId", user.Id.ToString() },
                { "email", request.NewEmail },
                { "token", token }
            }));

            var subject = Texts.ResourceManager.GetString(nameof(Texts.Subject), culture) ?? Texts.Subject;
            await mediator.Send(
                new SendEmail(
                    request.NewEmail,
                    user.FullName,
                    culture,
                    new ConfirmEmailChangeViewModel(user.FirstName, user.LastName, confirmUrl.AbsoluteUri, request.NewEmail, request.RequestedByAdmin),
                    subject,
                    BccRecipient: appSettings.BccRecipient),
                cancellationToken
            );

            return default;
        }
    }
}
