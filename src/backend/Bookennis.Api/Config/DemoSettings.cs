namespace Bookennis.Api.Config;

/// <summary>
/// The public demo (e.g. demo.clubnetz.app): two invented clubs that anybody can explore with known accounts. Environment variable
/// <c>Demo__Enabled</c>. When it is on, the app fills an empty database with the demo data at startup, wipes and fills it again every
/// night (<see cref="ResetTime"/>, Europe/Vienna) and switches off what visitors could misuse: registering, changing the password or
/// email of a demo account, deleting it and uploading files. See <c>Business/Demo</c>.
/// <para>
/// Never switch it on for a real installation. The reset refuses to run as soon as the database holds a user whose email is not a demo
/// address (<c>DemoAccounts.EmailDomain</c>), so it can't wipe real data by accident.
/// </para>
/// </summary>
public class DemoSettings
{
    public bool Enabled { get; set; }

    /// <summary>Local time (Europe/Vienna) of the nightly reset, as "HH:mm".</summary>
    public string ResetTime { get; set; } = "04:00";
}
