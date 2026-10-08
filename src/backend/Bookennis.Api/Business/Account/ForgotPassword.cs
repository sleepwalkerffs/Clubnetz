using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Config;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.User;
using Fusonic.Extensions.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Texts = Bookennis.Api.Resources.Localization.View_Emails_ForgotPassword;

namespace Bookennis.Api.Business.Account;

public record ForgotPassword([Required] string Email) : ICommand
{
    public class Handler(UserManager<User> userManager, IMediator mediator, AppSettings appSettings) : IRequestHandler<ForgotPassword>
    {
        public async Task<Unit> Handle(ForgotPassword request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
                return default;

            var culture = user.Language.ToCultureInfo();
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            if (token != null)
            {
                var url = appSettings.AppUri.AbsoluteUri.TrimEnd('/') + "/account/reset-password";
                var newUrl = new Uri(QueryHelpers.AddQueryString(url, new Dictionary<string, string?>() { { "email", user.Email }, { "token", token } }));
                await mediator.Send(
                    new SendEmail(
                        request.Email,
                        Texts.ResourceManager.GetString("Subject", culture) ?? Texts.Subject,
                        culture,
                        new ForgotPasswordEmailViewModel(newUrl.AbsoluteUri),
                        Texts.ResourceManager.GetString("Subject", culture) ?? Texts.Subject,
                        BccRecipient: appSettings.BccRecipient),
                    cancellationToken
                );
            }

            return default;
        }
    }
}
