using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Pages.Account;

public static partial class PasswordValidation
{
    public const int MinLength = 8;
    public const int MaxLength = 50;

    public record PasswordRule(string LocaleKey, Func<string, bool> IsSatisfied);

    /// <summary>Rules shown as a live checklist (<c>PasswordRequirements</c>). The length is validated separately via <c>StringLength</c>.</summary>
    public static IReadOnlyList<PasswordRule> Rules { get; } =
    [
        new("Requirement_Length", p => p.Length is >= MinLength and <= MaxLength),
        new("MustContainDigit", p => ContainsAtLeastOneDigit().IsMatch(p)),
        new("MustContainLower", p => ContainsLowerCaseLetter().IsMatch(p)),
        new("MustContainUpper", p => ContainsUpperCaseLetter().IsMatch(p)),
        new("Requirement_Special", p => ContainsSpecialCharacter().IsMatch(p))
    ];

    public static IEnumerable<string> ValidatePassword(string password, IStringLocalizer locale)
    {
        if (!ContainsAtLeastOneDigit().IsMatch(password))
        {
            yield return locale["MustContainDigit"];
        }

        if (!ContainsLowerCaseLetter().IsMatch(password))
        {
            yield return locale["MustContainLower"];
        }

        if (!ContainsUpperCaseLetter().IsMatch(password))
        {
            yield return locale["MustContainUpper"];
        }

        if (!ContainsSpecialCharacter().IsMatch(password))
        {
            yield return locale["MustContainSpecial"];
        }
    }

    [GeneratedRegex(".*\\d.*")]
    private static partial Regex ContainsAtLeastOneDigit();
    [GeneratedRegex(".*[a-z].*")]
    private static partial Regex ContainsLowerCaseLetter();
    [GeneratedRegex(".*[A-Z].*")]
    private static partial Regex ContainsUpperCaseLetter();
    [GeneratedRegex(@".*[*.!@#$%^&(){}[\]:;<>,.?\/~`_+\-=|\\].*")]
    private static partial Regex ContainsSpecialCharacter();
}
