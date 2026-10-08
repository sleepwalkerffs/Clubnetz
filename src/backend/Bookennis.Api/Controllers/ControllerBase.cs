using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers;

[ApiController]
[ApiVersionNeutral]
[Route("api/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class ControllerBase : Microsoft.AspNetCore.Mvc.ControllerBase;