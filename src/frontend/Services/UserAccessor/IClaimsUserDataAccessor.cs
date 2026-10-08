namespace Bookennis.Client.Services.UserAccessor;

public interface IClaimsUserDataAccessor
{
    public int? Id { get; }
    public string? Name { get; }
    public string? FirstName { get; }
    public string? LastName { get; }
    public string? Email { get; }
    public string? Salutation { get; }
}
