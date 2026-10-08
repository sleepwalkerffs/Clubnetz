using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.Shared;
using Bookennis.Api.Config;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Texts = Bookennis.Api.Resources.Localization.View_Emails_VerifyUser;

namespace Bookennis.Api.Business.Account;

public record RegisterUser(
    [Required] string FirstName,
    [Required] string LastName,
    [Required] DateOnly Birthday,
    [Required] string Email,
    [Required] string UserName,
    [Required] string Password,
    [Required] Gender Gender,
    string? Street,
    string? City,
    string? ZipCode,
    [Required] Country Country,
    bool AcceptPrivacyPolicy
) : ICommand
{
    public enum ErrorCode
    {
        PrivacyPolicyNotAccepted
    }

    public class Handler(UserManager<User> userManager, IMediator mediator, AppSettings appSettings) : IRequestHandler<RegisterUser>
    {
        public async Task<Unit> Handle(RegisterUser request, CancellationToken cancellationToken)
        {
            if (!request.AcceptPrivacyPolicy)
                throw new PreconditionException(ErrorCode.PrivacyPolicyNotAccepted, "The privacy policy has to be accepted.");

            var user = new User(request.UserName, request.Email, request.FirstName, request.LastName, request.Birthday, request.Gender, request.Street ?? string.Empty, request.City ?? string.Empty, request.ZipCode ?? string.Empty, request.Country);
            user.AcceptPrivacyPolicy();
            var result = await userManager.CreateAsync(user, request.Password);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, UserRoles.User.ToString());

                var culture = user.Language.ToCultureInfo();
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                if (token != null)
                {
                    var url = appSettings.AppUri.AbsoluteUri.TrimEnd('/') + "/Account/ConfirmEmail";
                    var newUrl = new Uri(QueryHelpers.AddQueryString(url, new Dictionary<string, string?>() { { "email", user.Email }, { "token", token } }));
                    await mediator.Send(
                        new SendEmail(
                            request.Email,
                            Texts.ResourceManager.GetString("Welcome", culture) ?? Texts.Welcome,
                            culture,
                            new VerifyUserEmailViewModel(user.FirstName, user.LastName, newUrl.AbsoluteUri),
                            Texts.ResourceManager.GetString("Welcome", culture) ?? Texts.Welcome,
                            BccRecipient: appSettings.BccRecipient),
                        cancellationToken
                    );
                }
                return default;
            }

            throw new PreconditionException(AccountErrorCode.IdentityError, result.Errors.Select(x => x.Code).ToArray(), "Error when changing the password.");
        }
    }
}