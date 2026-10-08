using System.Text;
using Bookennis.Domain.Exceptions;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using AngleSharp.Html.Dom;
using Ganss.Xss;
using Markdig;
using Microsoft.Extensions.FileProviders;

namespace Bookennis.Api.Business.ClubEmails;

public record RenderedClubEmail(string Subject, string BodyHtml);

public enum ClubEmailTemplateErrorCode
{
    InvalidEmailTemplateSyntax = 0,
    UnknownEmailTemplateVariable = 1,
    EmailTemplateRenderingFailed = 2
}

public interface IClubEmailRenderer
{
    /// <summary>Throws a <see cref="PreconditionException"/> if the template can not be parsed or uses unknown variables.</summary>
    void Validate(ClubEmailDefinition definition, ClubEmailTemplateContent template);

    Task<RenderedClubEmail> Render(ClubEmailTemplateContent template, ClubEmailVariables variables);

    /// <summary>
    /// Renders Markdown that is shown in the app (e.g. an announcement) with the same Markdown rules as the emails.
    /// Links open in a new tab and images are removed, because the app only loads images from itself.
    /// </summary>
    string RenderMarkdown(string markdown);

    /// <summary>The text of the Markdown without formatting, in one line.</summary>
    string ToPlainText(string markdown);
}

/// <summary>
/// Renders club email templates: Liquid (Fluid) -> Markdown (Markdig) -> sanitized HTML.
/// Templates only have access to the properties of the <see cref="ClubEmailVariables"/> records.
/// </summary>
public class ClubEmailRenderer : IClubEmailRenderer
{
    // Characters that could change the Markdown structure when they are part of a variable value
    // (e.g. a member name like "[click](https://evil)"). They are backslash escaped in the body.
    private const string MarkdownSpecialCharacters = "\\`*_{}[]()<>#!";

    private static readonly FluidParser Parser = new();
    private static readonly TemplateOptions SubjectOptions = CreateOptions(escapeMarkdown: false);
    private static readonly TemplateOptions BodyOptions = CreateOptions(escapeMarkdown: true);

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAutoLinks()
        .UseSoftlineBreakAsHardlineBreak()
        .UseGenericAttributes() // must be the last extension
        .Build();

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();
    private static readonly HtmlSanitizer AppContentSanitizer = CreateAppContentSanitizer();

    public void Validate(ClubEmailDefinition definition, ClubEmailTemplateContent template)
    {
        foreach (var (field, source) in new[] { ("Subject", template.Subject), ("Body", template.Body) })
        {
            var parsed = Parse(source, field);
            var unknown = new VariableCollector(definition.VariablePaths).Collect(parsed);
            if (unknown.Count > 0)
            {
                throw new PreconditionException(
                    ClubEmailTemplateErrorCode.UnknownEmailTemplateVariable,
                    [string.Join(", ", unknown)],
                    $"Unknown variables in {field}: {string.Join(", ", unknown)}");
            }
        }
    }

    public async Task<RenderedClubEmail> Render(ClubEmailTemplateContent template, ClubEmailVariables variables)
    {
        var subject = await RenderLiquid(Parse(template.Subject, "Subject"), variables, SubjectOptions);
        var markdown = await RenderLiquid(Parse(template.Body, "Body"), variables, BodyOptions);

        subject = string.Join(' ', subject.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var html = Sanitizer.Sanitize(Markdown.ToHtml(markdown, MarkdownPipeline));

        return new RenderedClubEmail(subject, html);
    }

    public string RenderMarkdown(string markdown)
        => AppContentSanitizer.Sanitize(Markdown.ToHtml(markdown, MarkdownPipeline));

    public string ToPlainText(string markdown)
        => string.Join(' ', Markdown.ToPlainText(markdown, MarkdownPipeline).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    internal static string EscapeMarkdown(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (MarkdownSpecialCharacters.Contains(c))
                builder.Append('\\');
            builder.Append(c);
        }

        return builder.ToString();
    }

    private static IFluidTemplate Parse(string source, string field)
    {
        if (!Parser.TryParse(source, out var template, out var error))
        {
            throw new PreconditionException(
                ClubEmailTemplateErrorCode.InvalidEmailTemplateSyntax,
                [$"{field}: {error}"],
                $"The email template {field} is invalid: {error}");
        }

        return template;
    }

    private static async Task<string> RenderLiquid(IFluidTemplate template, ClubEmailVariables variables, TemplateOptions options)
    {
        var context = new TemplateContext(options);
        foreach (var property in ClubEmailCatalog.GetExposedProperties(variables.GetType()))
            context.SetValue(ClubEmailCatalog.GetVariableName(property), property.GetValue(variables));

        try
        {
            return await template.RenderAsync(context, NullEncoder.Default);
        }
        catch (Exception e) when (e is FluidException or InvalidOperationException)
        {
            throw new PreconditionException(ClubEmailTemplateErrorCode.EmailTemplateRenderingFailed, [e.Message], $"The email template could not be rendered: {e.Message}");
        }
    }

    private static TemplateOptions CreateOptions(bool escapeMarkdown)
    {
        var options = new TemplateOptions
        {
            MaxSteps = 10_000,
            FileProvider = new NullFileProvider() // no {% include %} / {% render %} of files
        };
        options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.SnakeCase;
        foreach (var type in ClubEmailCatalog.GetExposedTypes())
            options.MemberAccessStrategy.Register(type);

        // Must be registered before the escaping of strings: this Markdown is inserted as it is
        options.ValueConverters.Add(value => value is MarkdownText markdown ? new StringValue(markdown.Value) : null);

        if (escapeMarkdown)
            options.ValueConverters.Add(value => value is string s ? new StringValue(EscapeMarkdown(s)) : null);

        return options;
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowedAttributes.Add("class");
        return sanitizer;
    }

    private static HtmlSanitizer CreateAppContentSanitizer()
    {
        var sanitizer = CreateSanitizer();
        sanitizer.AllowedTags.Remove("img");
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IHtmlAnchorElement anchor)
            {
                anchor.SetAttribute("target", "_blank");
                anchor.SetAttribute("rel", "noopener noreferrer");
            }
        };
        return sanitizer;
    }

    /// <summary>Collects all variables a template reads that are not part of the email's variables.</summary>
    private sealed class VariableCollector(IReadOnlyList<string> knownPaths) : AstVisitor
    {
        // Liquid built-ins that are valid everywhere, and properties Liquid provides on every value.
        private static readonly HashSet<string> BuiltInRoots = ["forloop", "tablerowloop", "empty", "blank", "nil", "null", "true", "false"];
        private static readonly HashSet<string> BuiltInProperties = ["size", "first", "last"];

        private readonly HashSet<string> knownPrefixes = knownPaths
            .SelectMany(path =>
            {
                var segments = path.Split('.');
                return segments.Select((_, i) => string.Join('.', segments.Take(i + 1)));
            })
            .ToHashSet();

        private readonly HashSet<string> localVariables = [];
        private readonly SortedSet<string> unknown = [];

        public List<string> Collect(IFluidTemplate template)
        {
            VisitTemplate(template);
            // Variables can be read before they are assigned in the source order, so filter at the end.
            return unknown.Where(name => !localVariables.Contains(name.Split('.')[0])).ToList();
        }

        protected override Statement VisitAssignStatement(AssignStatement assignStatement)
        {
            localVariables.Add(assignStatement.Identifier);
            return base.VisitAssignStatement(assignStatement);
        }

        protected override Statement VisitCaptureStatement(CaptureStatement captureStatement)
        {
            localVariables.Add(captureStatement.Identifier);
            return base.VisitCaptureStatement(captureStatement);
        }

        protected override Statement VisitForStatement(ForStatement forStatement)
        {
            localVariables.Add(forStatement.Identifier);
            return base.VisitForStatement(forStatement);
        }

        protected override Expression VisitMemberExpression(MemberExpression memberExpression)
        {
            var path = string.Empty;
            foreach (var segment in memberExpression.Segments)
            {
                if (segment is not IdentifierSegment identifier)
                    break;

                if (path.Length == 0 && BuiltInRoots.Contains(identifier.Identifier))
                    break;
                if (path.Length > 0 && knownPaths.Contains(path) && BuiltInProperties.Contains(identifier.Identifier))
                    break;

                path = path.Length == 0 ? identifier.Identifier : $"{path}.{identifier.Identifier}";
                if (!knownPrefixes.Contains(path))
                {
                    unknown.Add(path);
                    break;
                }
            }

            return base.VisitMemberExpression(memberExpression);
        }
    }
}
