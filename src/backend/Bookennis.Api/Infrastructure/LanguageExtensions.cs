using System.Globalization;
using Bookennis.Api.Business.Clubs;
using Bookennis.Domain.User;
using Bookennis.Shared.Utils;

namespace Bookennis.Api.Infrastructure;

public static class LanguageExtensions
{
    /// <summary>The culture of the language outside a club (account emails, request localization): Austrian conventions.</summary>
    public static CultureInfo ToCultureInfo(this Language language) => language switch
    {
        Language.German => new CultureInfo("de-AT"),
        Language.English => new CultureInfo("en-AT"),
        _ => new CultureInfo("de-AT")
    };

    /// <summary>The culture of a text about a club: the reader's language with the formats and currency of the club's country.</summary>
    public static RegionalCulture ToCultureInfo(this Language language, ClubRegion region) => region.Culture(language);
}
