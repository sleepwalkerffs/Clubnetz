using System.Globalization;

namespace Bookennis.Api.Infrastructure;

public interface IUserLanguageAccessor
{
    public CultureInfo GetCultureInfo();
    public string GetLanguage();
}
