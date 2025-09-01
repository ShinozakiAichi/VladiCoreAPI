using Microsoft.AspNetCore.Mvc;

namespace VladiCore.Api.Controllers;

[ApiController]
[Route("ping")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("pong");
}
