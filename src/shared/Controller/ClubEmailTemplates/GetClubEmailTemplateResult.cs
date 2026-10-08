using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.ClubEmailTemplates;

public record GetClubEmailTemplateResult
{
    public required ClubEmailType Type { get; init; }
    public required List<ClubEmailTemplateLanguageDto> Languages { get; init; }
}

public record ClubEmailTemplateLanguageDto
{
    public required Language Language { get; init; }
    public required bool IsCustomized { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public required string DefaultSubject { get; init; }
    public required string DefaultBody { get; init; }
    public required List<ClubEmailVariableDto> Variables { get; init; }
}

public record ClubEmailVariableDto
{
    /// <summary>The Liquid path, e.g. <c>member.first_name</c>.</summary>
    public required string Name { get; init; }
    public required string SampleValue { get; init; }
}
