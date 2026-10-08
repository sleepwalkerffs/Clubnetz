using System.Globalization;
using Bookennis.Api.Config;
using Fusonic.Extensions.AspNetCore.Http;

namespace Bookennis.Api.Infrastructure;

public class UserLanguageAccessor(IHttpContextAccessor httpContextAccessor) : IUserLanguageAccessor
{
    public CultureInfo GetCultureInfo()
    {
        var language = httpContextAccessor.HttpContext?.Request
                                                       .GetTypedHeaders().AcceptLanguage?.OrderByDescending(x => x.Quality ?? 1)
                                                       .Select(x => x.Value.ToString())
                                                       .FirstOrDefault();

        if (language is not null && CultureUtil.GetFirstSupportedCulture(language, AppSettings.SupportedCultures) is CultureInfo cultureInfo)
            return cultureInfo;

        return AppSettings.DefaultCulture;
    }

    public string GetLanguage()
        => GetCultureInfo().TwoLetterISOLanguageName;
}
