using Fusonic.Extensions.Email;

namespace Bookennis.Api.Business.Admin;

[EmailView("Emails/ConfirmEmailChange")]
public record ConfirmEmailChangeViewModel(string FirstName, string LastName, string Url, string NewEmail, bool RequestedByAdmin);
