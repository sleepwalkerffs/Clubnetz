using System.Diagnostics.CodeAnalysis;

namespace Bookennis.Api.Business;

public static class QueryUtil
{
    public const string EscapeCharacter = "\\";

    public static string? Escape([NotNullIfNotNull(nameof(filter))] string? filter)
        => string.IsNullOrWhiteSpace(filter) ? null : $"%{filter.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
}