using Fusonic.Extensions.Email;

namespace Bookennis.Api.Business.Account;

[EmailView("Emails/ForgotPassword")]
public record ForgotPasswordEmailViewModel(string Url);
