using Bookennis.Api.Business.Account;
using Bookennis.Api.Config;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.User;
using Fusonic.Extensions.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Texts = Bookennis.Api.Resources.Localization.View_Emails_VerifyUser;

namespace Bookennis.Api.Business.Admin;

public record ResendConfirmationEmail(int UserId) : ICommand
{
    public class Handler(UserManager<User> userManager, IMediator mediator, AppSettings appSettings) : IRequestHandler<ResendConfirmationEmail>
    {
        public async Task<Unit> Handle(ResendConfirmationEmail request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.UserId.ToString())
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(User), request.UserId);

            if (user.EmailConfirmed)
                return default;

            var culture = user.Language.ToCultureInfo();
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var url = appSettings.AppUri.AbsoluteUri.TrimEnd('/') + "/Account/ConfirmEmail";
            var newUrl = new Uri(QueryHelpers.AddQueryString(url, new Dictionary<string, string?>() { { "email", user.Email }, { "token", token } }));

            await mediator.Send(
                new SendEmail(
                    user.Email!,
                    Texts.ResourceManager.GetString("Welcome", culture) ?? Texts.Welcome,
                    culture,
                    new VerifyUserEmailViewModel(user.FirstName, user.LastName, newUrl.AbsoluteUri),
                    Texts.ResourceManager.GetString("Welcome", culture) ?? Texts.Welcome,
                    BccRecipient: appSettings.BccRecipient),
                cancellationToken
            );

            return default;
        }
    }
}
