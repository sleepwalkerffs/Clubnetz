using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.Profile;

public record DeleteAccountModel([Required] string CurrentPassword);
