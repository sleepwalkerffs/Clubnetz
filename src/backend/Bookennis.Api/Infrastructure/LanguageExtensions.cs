using System.Globalization;
using Bookennis.Domain.User;

namespace Bookennis.Api.Infrastructure;

public static class LanguageExtensions
{
    public static CultureInfo ToCultureInfo(this Language language) => language switch
    {
        Language.German => new CultureInfo("de-AT"),
        Language.English => new CultureInfo("en-AT"),
        _ => new CultureInfo("de-AT")
    };
}
