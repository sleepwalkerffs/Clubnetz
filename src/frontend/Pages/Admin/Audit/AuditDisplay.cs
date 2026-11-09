using Bookennis.Client.Localization.Pages.Admin;
using Bookennis.Shared.Controller.Admin.Audit;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Pages.Admin.Audit;

/// <summary>How the entries of the audit log are shown in the list and the detail dialog.</summary>
public static class AuditDisplay
{
    private static readonly HashSet<string> DataActions = ["Created", "Modified", "Deleted"];

    public static string KindEmoji(AuditEventKind kind) => kind switch
    {
        AuditEventKind.DataChange => "📝",
        AuditEventKind.Command => "⚡",
        AuditEventKind.Security => "🔐",
        AuditEventKind.Read => "👁️",
        _ => "•"
    };

    /// <summary>"Booking #1234 · Modified" for data changes, the name of the command otherwise.</summary>
    public static string Title(AuditEventListItem e, IStringLocalizer<AdminLocale> l)
    {
        if (e.Kind != AuditEventKind.DataChange)
            return e.Action == "AccessDenied" ? $"{l["AuditAccessDenied"]}{(e.Request is null ? "" : $" · {e.Request}")}" : e.Action;

        var action = DataActions.Contains(e.Action) ? l[$"AuditAction_{e.Action}"].Value : e.Action;
        return $"{e.EntityType}{(e.EntityId is null ? "" : $" #{e.EntityId}")} · {action}";
    }

    /// <summary>Who did it: the user, the background job, or nobody signed in.</summary>
    public static string Actor(AuditEventListItem e, IStringLocalizer<AdminLocale> l)
        => e.ActorName ?? (e.ActorUserId is { } id ? $"#{id}" : null) ?? (e.System is { } system ? $"⚙️ {system}" : l["AuditAnonymous"]);

    /// <summary>Error codes like <c>LoginUser.ErrorCode.InvalidCredentials</c> are long: the last part says it.</summary>
    public static string Short(string errorCode)
    {
        var dot = errorCode.LastIndexOf('.');
        return dot < 0 || dot == errorCode.Length - 1 ? errorCode : errorCode[(dot + 1)..];
    }
}
