using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.ClubEmailTemplates;

public record GetClubEmailTemplatesResult
{
    public required List<ClubEmailTemplateSummaryDto> Templates { get; init; }
    public required ClubEmailSettingsDto Settings { get; init; }
}

public record ClubEmailTemplateSummaryDto
{
    public required ClubEmailType Type { get; init; }
    public required List<Language> CustomizedLanguages { get; init; }
}

public record ClubEmailSettingsDto
{
    public string? WebsiteUrl { get; init; }
    public string? ReplyToEmail { get; init; }
}
