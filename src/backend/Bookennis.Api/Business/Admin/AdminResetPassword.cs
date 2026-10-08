using Bookennis.Api.Business.Account;
using Bookennis.Api.Config;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.User;
using Fusonic.Extensions.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Texts = Bookennis.Api.Resources.Localization.View_Emails_ForgotPassword;

namespace Bookennis.Api.Business.Admin;

public record AdminResetPassword(int UserId) : ICommand
{
    public class Handler(UserManager<User> userManager, IMediator mediator, AppSettings appSettings) : IRequestHandler<AdminResetPassword>
    {
        public async Task<Unit> Handle(AdminResetPassword request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.UserId.ToString())
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(User), request.UserId);

            var culture = user.Language.ToCultureInfo();
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var url = appSettings.AppUri.AbsoluteUri.TrimEnd('/') + "/account/reset-password";
            var newUrl = new Uri(QueryHelpers.AddQueryString(url, new Dictionary<string, string?>() { { "email", user.Email }, { "token", token } }));

            await mediator.Send(
                new SendEmail(
                    user.Email!,
                    Texts.ResourceManager.GetString("Subject", culture) ?? Texts.Subject,
                    culture,
                    new ForgotPasswordEmailViewModel(newUrl.AbsoluteUri),
                    Texts.ResourceManager.GetString("Subject", culture) ?? Texts.Subject,
                    BccRecipient: appSettings.BccRecipient),
                cancellationToken
            );

            return default;
        }
    }
}
