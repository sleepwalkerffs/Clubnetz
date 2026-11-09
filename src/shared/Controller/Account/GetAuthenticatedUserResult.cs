namespace Bookennis.Shared.Controller.Account;

/// <param name="ImpersonatorName">The application administrator who is signed in as this user, if any.</param>
public record GetAuthenticatedUserResult(int Id, string FirstName, string LastName, string Email, string Name, string[] Roles, string? ImpersonatorName = null);
