using Bookennis.Global;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Families;

public record UpdateOwnedChildRequest(string FirstName, string LastName, DateOnly Birthday, Gender Gender)
{
    public string FirstName { get; init; } = FirstName.Clean();
    public string LastName { get; init; } = LastName.Clean();
}
