using System.Globalization;
using System.Text;

namespace Bookennis.Api.Business.Demo;

/// <summary>The accounts of the demo. All of them share one public password, it is shown on the login page of the demo.</summary>
public static class DemoAccounts
{
    /// <summary>Every demo user has an address of this domain. It belongs to Clubnetz, so no stranger ever gets a demo email.</summary>
    public const string EmailDomain = "demo.clubnetz.app";

    public const string Password = "Clubnetz-Demo1!";

    public static bool IsDemoEmail(string? email)
        => email is not null && email.EndsWith("@" + EmailDomain, StringComparison.OrdinalIgnoreCase);

    /// <summary>"Jürgen Müller" → "juergen.mueller@demo.clubnetz.app".</summary>
    public static string EmailFor(string firstName, string lastName) => $"{Local(firstName)}.{Local(lastName)}@{EmailDomain}";

    private static string Local(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            switch (c)
            {
                case 'ä': builder.Append("ae"); break;
                case 'ö': builder.Append("oe"); break;
                case 'ü': builder.Append("ue"); break;
                case 'ß': builder.Append("ss"); break;
                case >= 'a' and <= 'z' or >= '0' and <= '9': builder.Append(c); break;
                default:
                    var plain = c.ToString().Normalize(NormalizationForm.FormD).FirstOrDefault(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark);
                    if (plain is >= 'a' and <= 'z')
                        builder.Append(plain);
                    break;
            }
        }

        return builder.ToString();
    }
}
