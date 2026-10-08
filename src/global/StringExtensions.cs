namespace Bookennis.Global;

public static class StringExtensions
{
    public static string? CleanNullable(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string Clean(this string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is empty") : value.Trim();
}