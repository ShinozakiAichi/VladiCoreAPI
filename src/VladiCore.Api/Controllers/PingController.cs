using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace VladiCore.Api.Controllers;

[ApiController]
[Route("ping")]
[AllowAnonymous]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("pong");
}
