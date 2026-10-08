namespace Bookennis.Shared.Controller.Admin;

public record ChangeUserEmailRequest
{
    public required string NewEmail { get; init; }
}
