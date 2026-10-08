using Microsoft.AspNetCore.Components;

namespace Bookennis.Client.Services;

public static class NavigationManagerExtensions
{
    public static string BaseApiUrl(this NavigationManager nav) => nav.BaseUri.TrimEnd('/') + "/api/";
    public static string BaseUrl(this NavigationManager nav) => nav.BaseUri.TrimEnd('/') + "/";

    public static void NavigateToLogin(this NavigationManager nav) => nav.NavigateTo("/account/login");

    /// <summary>Navigates to the login and returns to the current page afterwards (e.g. an event link shared via WhatsApp).</summary>
    public static void NavigateToLoginAndReturn(this NavigationManager nav)
    {
        var relativePath = "/" + nav.ToBaseRelativePath(nav.Uri);
        if (relativePath == "/" || relativePath.StartsWith("/account/", StringComparison.OrdinalIgnoreCase))
        {
            nav.NavigateToLogin();
            return;
        }

        nav.NavigateTo($"/account/login?returnUrl={Uri.EscapeDataString(relativePath)}");
    }

    /// <summary>Only allows relative paths of this app as return url, so the login can't be abused as an open redirect.</summary>
    public static bool IsLocalReturnUrl(string? url)
        => !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");

    public static void NavigateToAccessDenied(this NavigationManager nav) => nav.NavigateTo("/accessdenied");
    public static void NavigateToInternalError(this NavigationManager nav) => nav.NavigateTo("/error");
    public static void NavigateToNotFound(this NavigationManager nav) => nav.NavigateTo("/notfound");
    public static void NavigateToProfile(this NavigationManager nav) => nav.NavigateTo("account/profile");
    public static void NavigateToClub(this NavigationManager nav, int clubId) => nav.NavigateTo($"/clubs/{clubId}/my-club");
    public static void NavigateToClubEvent(this NavigationManager nav, int clubId, int clubEventId) => nav.NavigateTo($"/clubs/{clubId}/calendar/{clubEventId}");
    public static void NavigateToBooking(this NavigationManager nav, int clubId, int bookingEntryId) => nav.NavigateTo($"/clubs/{clubId}/booking/{bookingEntryId}");
    public static void NavigateToMember(this NavigationManager nav, int clubId, int memberId) => nav.NavigateTo($"/clubs/{clubId}/members/{memberId}");
    public static void NavigateToAdmin(this NavigationManager nav) => nav.NavigateTo("/admin/users");
    public static void NavigateToAdminUser(this NavigationManager nav, int userId) => nav.NavigateTo($"/admin/users/{userId}");
    public static void NavigateToAdminClub(this NavigationManager nav, int clubId) => nav.NavigateTo($"/admin/clubs/{clubId}");
}