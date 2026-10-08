using System.Collections.Concurrent;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.User;

namespace Bookennis.Api.Business.ClubEmails;

public record ClubEmailTemplateContent(string Subject, string Body);

/// <summary>
/// The built-in, club neutral templates that are used when a club has not customized an email.
/// They are stored as embedded resources in <c>Resources/EmailTemplates/{Type}.{Language}.md</c>:
/// the first line is the subject, the rest is the body.
/// </summary>
public static class ClubEmailDefaultTemplates
{
    private static readonly ConcurrentDictionary<(ClubEmailType, Language), ClubEmailTemplateContent> Cache = new();

    public static ClubEmailTemplateContent Get(ClubEmailType type, Language language)
        => Cache.GetOrAdd((type, language), key => Load(key.Item1, key.Item2));

    private static ClubEmailTemplateContent Load(ClubEmailType type, Language language)
    {
        var assembly = typeof(ClubEmailDefaultTemplates).Assembly;
        var resourceName = $"{assembly.GetName().Name}.Resources.EmailTemplates.{type}.{language}.md";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Default email template '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd().Replace("\r\n", "\n");

        var lineBreak = content.IndexOf('\n');
        if (lineBreak < 0)
            throw new InvalidOperationException($"Default email template '{resourceName}' has no body.");

        return new ClubEmailTemplateContent(content[..lineBreak].Trim(), content[(lineBreak + 1)..].Trim('\n'));
    }
}
