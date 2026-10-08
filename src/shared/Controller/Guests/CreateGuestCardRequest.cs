using System.ComponentModel.DataAnnotations;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Guests;

public record CreateGuestCardRequest(int PurchasedBookings, [EmailAddress] string Email, string FirstName, string LastName, DateOnly Birthday, Gender Gender);

