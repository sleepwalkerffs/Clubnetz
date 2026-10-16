using Bookennis.Shared.Controller.ClubEmailTemplates;

namespace Bookennis.Client.Pages.ClubShell.Club.EmailTemplates;

public static class EmailTypeInfo
{
    public static string Emoji(ClubEmailType type) => ClubEmailTypeEmoji.For(type);

    public static string NameKey(ClubEmailType type) => $"Type_{type}";

    public static string DescriptionKey(ClubEmailType type) => $"TypeDescription_{type}";
}
