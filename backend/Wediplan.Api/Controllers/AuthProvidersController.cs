using Microsoft.AspNetCore.Mvc;

namespace Wediplan.Api.Controllers;

/// <summary>GET /api/auth/providers — koji su načini prijave uključeni (frontend skriva gumbe kojih nema).</summary>
[ApiController]
[Route("api/auth/providers")]
public class AuthProvidersController : ControllerBase
{
    private readonly IConfiguration _cfg;
    public AuthProvidersController(IConfiguration cfg) => _cfg = cfg;

    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        password = true,
        magicLink = true,
        google = !string.IsNullOrWhiteSpace(_cfg["Google:ClientId"]),
    });
}
