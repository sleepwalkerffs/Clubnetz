using System.Diagnostics.CodeAnalysis;
using Bookennis.Domain.Base;
using Bookennis.Domain.User;

namespace Bookennis.Domain.Clubs.EmailTemplates;

/// <summary>
/// A club specific version of a <see cref="ClubEmailType"/> in one language.
/// Subject and body are Liquid templates, the body is Markdown.
/// If a club has no template for a type, the built-in default is used.
/// </summary>
public class ClubEmailTemplate : TenantDomainEntity
{
    public const int SubjectMaxLength = 200;
    public const int BodyMaxLength = 20000;

#pragma warning disable CS8618
    private ClubEmailTemplate() { }
#pragma warning restore CS8618

    public ClubEmailTemplate(int clubId, ClubEmailType type, Language language, string subject, string body)
    {
        ClubId = clubId;
        Type = type;
        Language = language;
        Update(subject, body);
    }

    public ClubEmailType Type { get; private set; }
    public Language Language { get; private set; }
    public string Subject { get; private set; }
    public string Body { get; private set; }

    [MemberNotNull(nameof(Subject), nameof(Body))]
    public void Update(string subject, string body)
    {
        subject = subject.Trim();
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject must not be empty.", nameof(subject));
        if (subject.Length > SubjectMaxLength)
            throw new ArgumentException($"Subject must not exceed {SubjectMaxLength} characters.", nameof(subject));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body must not be empty.", nameof(body));
        if (body.Length > BodyMaxLength)
            throw new ArgumentException($"Body must not exceed {BodyMaxLength} characters.", nameof(body));

        Subject = subject;
        Body = body;
    }
}
