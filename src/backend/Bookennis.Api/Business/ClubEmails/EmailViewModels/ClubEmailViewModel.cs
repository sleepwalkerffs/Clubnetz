using Fusonic.Extensions.Email;

namespace Bookennis.Api.Business.ClubEmails.EmailViewModels;

/// <summary>Generic layout for all club emails. <see cref="BodyHtml"/> is rendered and sanitized by <see cref="ClubEmailRenderer"/>.</summary>
[EmailView("Emails/ClubEmail")]
public record ClubEmailViewModel(string Title, string BodyHtml);
