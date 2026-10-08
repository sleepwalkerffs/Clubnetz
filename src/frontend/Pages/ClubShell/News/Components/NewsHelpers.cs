using System.Globalization;
using Bookennis.Client.Localization.Pages.News;
using Bookennis.Shared.Controller.ClubAnnouncements;
using Bookennis.Shared.Controller.Shared;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Bookennis.Client.Pages.ClubShell.News.Components;

internal static class NewsHelpers
{
    public static readonly ClubAnnouncementAudience[] Audiences =
        [ClubAnnouncementAudience.AllMembers, ClubAnnouncementAudience.ActiveSeasonMembers, ClubAnnouncementAudience.Youth, ClubAnnouncementAudience.Roles];

    /// <summary>The roles an announcement can be sent to. Every member has the role <c>User</c>, so it is not listed.</summary>
    public static readonly MemberRole[] EmailRoles =
        [MemberRole.Admin, MemberRole.SportsDirector, MemberRole.YouthSportsDirector, MemberRole.Trainer, MemberRole.Maintainer, MemberRole.Treasurer];

    /// <summary>Roles that write announcements, same as the backend policy <c>ClubAnnouncementManager</c>.</summary>
    public static bool CanManage(IEnumerable<MemberRole> roles)
        => roles.Any(r => r is MemberRole.SportsDirector or MemberRole.YouthSportsDirector or MemberRole.Admin);

    public static string NewsUrl(int clubId) => $"/clubs/{clubId}/news";

    public static string AnnouncementUrl(int clubId, int clubAnnouncementId) => $"/clubs/{clubId}/news/{clubAnnouncementId}";

    public static string AudienceName(IStringLocalizer<NewsLocale> locale, ClubAnnouncementAudience audience) => locale[$"Audience_{audience}"];

    public static string AudienceEmoji(ClubAnnouncementAudience audience) => audience switch
    {
        ClubAnnouncementAudience.AllMembers => "👥",
        ClubAnnouncementAudience.ActiveSeasonMembers => "🎾",
        ClubAnnouncementAudience.Youth => "🧒",
        _ => "🏷️",
    };

    /// <summary>"Today", "Yesterday" or the date (the format is localized).</summary>
    public static string FormatPublished(IStringLocalizer<NewsLocale> locale, DateTimeOffset publishedAt)
    {
        var date = DateOnly.FromDateTime(publishedAt.LocalDateTime);
        var days = DateOnly.FromDateTime(DateTime.Today).DayNumber - date.DayNumber;
        return days switch
        {
            0 => locale["Today"],
            1 => locale["Yesterday"],
            _ => FormatDate(locale, date),
        };
    }

    public static string FormatDate(IStringLocalizer<NewsLocale> locale, DateOnly date) => date.ToString(locale["DateFormat"], CultureInfo.CurrentCulture);

    public static string FormatDateTime(IStringLocalizer<NewsLocale> locale, DateTimeOffset value)
        => value.LocalDateTime.ToString(locale["DateFormat"] + ", HH:mm", CultureInfo.CurrentCulture);

    /// <summary>Formats a count with the "{key}One" text for exactly one, e.g. "1 attachment" vs. "3 attachments".</summary>
    public static string Count(IStringLocalizer<NewsLocale> locale, string key, int count)
        => string.Format(locale[count == 1 ? key + "One" : key], count);

    /// <summary>E.g. "340 KB" or "1.2 MB".</summary>
    public static string FormatSize(long bytes)
        => bytes < 1024 * 1024
            ? $"{Math.Max(1, (int)Math.Round(bytes / 1024.0))} KB"
            : $"{(bytes / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.CurrentCulture)} MB";

    public static string FileIcon(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => Icons.Material.Filled.PictureAsPdf,
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => Icons.Material.Filled.Image,
        ".xls" or ".xlsx" or ".ods" or ".csv" => Icons.Material.Filled.TableChart,
        ".ppt" or ".pptx" => Icons.Material.Filled.Slideshow,
        ".ics" => Icons.Material.Filled.Event,
        _ => Icons.Material.Filled.Description,
    };

    /// <summary>The value of the <c>accept</c> attribute of the file input.</summary>
    public static string AcceptedFileTypes => string.Join(',', ClubAnnouncementLimits.AllowedFileExtensions);

    public static bool IsAllowedFile(string fileName)
        => ClubAnnouncementLimits.AllowedFileExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
}
