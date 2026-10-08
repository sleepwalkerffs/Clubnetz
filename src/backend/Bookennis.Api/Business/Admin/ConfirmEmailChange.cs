using Bookennis.Api.Config;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Email;
using Microsoft.AspNetCore.Identity;
using Texts = Bookennis.Api.Resources.Localization.View_Emails_EmailChanged;

namespace Bookennis.Api.Business.Admin;

public record ConfirmEmailChange(int UserId, string NewEmail, string Token) : ICommand
{
    public enum ErrorCode
    {
        InvalidToken,
        UserNotFound
    }

    public class Handler(UserManager<User> userManager, IMediator mediator, AppSettings appSettings) : IRequestHandler<ConfirmEmailChange>
    {
        public async Task<Unit> Handle(ConfirmEmailChange request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.UserId.ToString())
                ?? throw new PreconditionException(ErrorCode.UserNotFound, "User not found.");

            // The link was already used (e.g. clicked twice): nothing left to do
            if (string.Equals(user.Email, request.NewEmail, StringComparison.OrdinalIgnoreCase) && user.EmailConfirmed)
                return default;

            // ChangeEmailAsync overwrites user.Email, so remember the old values first
            var oldEmail = user.Email;
            var oldUserName = user.UserName;

            var result = await userManager.ChangeEmailAsync(user, request.NewEmail, request.Token);
            if (!result.Succeeded)
                throw new PreconditionException(ErrorCode.InvalidToken, "The email change token is invalid or has expired.");

            // Also update the username to match the new email if it was previously the email
            if (string.IsNullOrEmpty(oldUserName) || string.Equals(oldUserName, oldEmail, StringComparison.OrdinalIgnoreCase))
            {
                await userManager.SetUserNameAsync(user, request.NewEmail);
            }

            if (!string.IsNullOrWhiteSpace(oldEmail) && !string.Equals(oldEmail, request.NewEmail, StringComparison.OrdinalIgnoreCase))
                await NotifyOldEmail(user, oldEmail, request.NewEmail, cancellationToken);

            return default;
        }

        private async Task NotifyOldEmail(User user, string oldEmail, string newEmail, CancellationToken cancellationToken)
        {
            var culture = user.Language.ToCultureInfo();
            var subject = Texts.ResourceManager.GetString(nameof(Texts.Subject), culture) ?? Texts.Subject;
            var forgotPasswordUrl = appSettings.AppUri.AbsoluteUri.TrimEnd('/') + "/account/forgot-password";

            await mediator.Send(
                new SendEmail(
                    oldEmail,
                    user.FullName,
                    culture,
                    new EmailChangedViewModel(user.FirstName, user.LastName, newEmail, forgotPasswordUrl),
                    subject,
                    BccRecipient: appSettings.BccRecipient),
                cancellationToken
            );
        }
    }
}
