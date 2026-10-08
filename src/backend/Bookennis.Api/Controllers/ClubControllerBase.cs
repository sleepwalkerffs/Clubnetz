using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers;

[Route("api/Clubs/{clubId:int}/[controller]")]
public class ClubControllerBase : ControllerBase;