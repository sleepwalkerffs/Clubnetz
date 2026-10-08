using Fusonic.Extensions.Email;

namespace Bookennis.Api.Business.Account;

[EmailView("Emails/VerifyUser")]
public record VerifyUserEmailViewModel(string FirstName, string LastName, string Url);
