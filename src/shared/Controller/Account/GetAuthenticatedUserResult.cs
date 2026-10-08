namespace Bookennis.Shared.Controller.Account;

public record GetAuthenticatedUserResult(int Id, string FirstName, string LastName, string Email, string Name, string[] Roles);
