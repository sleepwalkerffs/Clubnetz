using Fusonic.Extensions.Email;

namespace Bookennis.Api.Business.ClubEmails.EmailViewModels;

/// <summary>
/// Generic layout for all club emails. <see cref="BodyHtml"/> is rendered and sanitized by <see cref="ClubEmailRenderer"/>.
/// <see cref="Emoji"/> is shown in the header (the emoji of the email type, like in the template list of the app).
/// </summary>
[EmailView("Emails/ClubEmail")]
public record ClubEmailViewModel(string Title, string BodyHtml, string ClubName, string Emoji);
