using Fusonic.Extensions.Email;

namespace Bookennis.Api.Business.Admin;

[EmailView("Emails/EmailChanged")]
public record EmailChangedViewModel(string FirstName, string LastName, string NewEmail, string ForgotPasswordUrl);
