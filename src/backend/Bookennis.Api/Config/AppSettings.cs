using System.Collections.Immutable;
using System.Globalization;

namespace Bookennis.Api.Config;

public class AppSettings
{
    public string ConnectionString { get; set; } = null!;
    public string AppUrl
    {
        get => AppUri.AbsoluteUri;
        set => AppUri = new Uri(value);
    }

    /// <summary>Optional address that gets a blind copy of every email the app sends. Not set = no copy.</summary>
    public string? BccRecipient { get; set; }

    public Uri AppUri { get; private set; } = new("http://notvalid.com");

    public PushSettings Push { get; set; } = new();

    public LegalSettings Legal { get; set; } = new();

    public static IReadOnlyList<CultureInfo> SupportedCultures
        => new[] { "de-AT", "en-AT" }
          .Select(c => new CultureInfo(c))
          .ToImmutableList();

    public static CultureInfo DefaultCulture => new("de-AT");
}
