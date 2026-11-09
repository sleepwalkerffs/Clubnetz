namespace Bookennis.Shared.Controller.Admin.Audit;

/// <summary>What an entry of the audit log records.</summary>
public enum AuditEventKind
{
    /// <summary>A row of the database was created, changed or deleted.</summary>
    DataChange,

    /// <summary>Somebody (or a background job) ran a command: what, by whom and whether it worked.</summary>
    Command,

    /// <summary>A command or check that is about accounts and access: sign-in, password, email, impersonation, API keys, access denied.</summary>
    Security,

    /// <summary>Somebody read sensitive data (exports).</summary>
    Read
}

public enum AuditOutcome
{
    Succeeded,
    Failed,
    Denied
}
