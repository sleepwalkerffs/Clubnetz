namespace Bookennis.Shared.Controller.Demo;

/// <summary>Whether this installation is the public demo, and the accounts its login page offers.</summary>
public record GetDemoInfoResult
{
    public bool IsEnabled { get; init; }

    /// <summary>The password all demo accounts share (public). <c>null</c> outside the demo.</summary>
    public string? Password { get; init; }

    public IReadOnlyList<DemoClubDto> Clubs { get; init; } = [];
}

public record DemoClubDto
{
    public string Name { get; init; } = "";

    /// <summary>"eu" or "us".</summary>
    public string Region { get; init; } = "";

    public IReadOnlyList<DemoAccountDto> Accounts { get; init; } = [];
}

public record DemoAccountDto
{
    /// <summary>Admin, Member, Parent, Trainer, Treasurer, ClubhouseHost or TwoClubs: the FE describes it.</summary>
    public string Persona { get; init; } = "";

    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
}
