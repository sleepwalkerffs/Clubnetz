using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.Profile;

public record ChangeEmailModel([Required] string CurrentPassword, [Required, EmailAddress] string NewEmail);
