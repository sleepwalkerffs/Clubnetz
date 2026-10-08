using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.Profile;

public record ChangePasswordModel([Required] string CurrentPassword, [Required] string NewPassword);
