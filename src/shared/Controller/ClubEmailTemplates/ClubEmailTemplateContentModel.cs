using System.ComponentModel.DataAnnotations;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.ClubEmailTemplates;

public record ClubEmailTemplateContentModel
{
    public required Language Language { get; init; }

    [Required]
    [MaxLength(200)]
    public required string Subject { get; init; }

    [Required]
    [MaxLength(20000)]
    public required string Body { get; init; }
}

public record UpdateClubEmailTemplateModel
{
    /// <summary>The customized languages. Languages that are not part of the list use the default template.</summary>
    [Required]
    public required List<ClubEmailTemplateContentModel> Languages { get; init; }
}

public record PreviewClubEmailTemplateResult
{
    public required string Subject { get; init; }
    public required string Html { get; init; }
}

public record UpdateClubEmailSettingsModel
{
    [MaxLength(500)]
    public string? WebsiteUrl { get; init; }

    [MaxLength(256)]
    [EmailAddress]
    public string? ReplyToEmail { get; init; }
}
