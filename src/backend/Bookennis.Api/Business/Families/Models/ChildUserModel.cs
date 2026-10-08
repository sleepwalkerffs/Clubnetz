using Bookennis.Domain.User;

namespace Bookennis.Api.Business.Families.Models;

public record ChildUserModel(string FirstName, string LastName, DateOnly Birthday, Gender Gender);
