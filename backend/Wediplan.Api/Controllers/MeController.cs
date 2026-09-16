using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Wediplan.Api.Contracts;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Controllers;

/// <summary>GET /api/me — trenutni korisnik iz sesije (cookie) ili 401. Frontend ga zove pri boot-u.</summary>
[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    public MeController(UserManager<AppUser> users) => _users = users;

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<MeDto>> Get()
    {
        var id = _users.GetUserId(User);
        var user = id != null ? await _users.FindByIdAsync(id) : null;
        if (user == null) return Unauthorized();
        var roles = await _users.GetRolesAsync(user);
        return new MeDto(user.Id.ToString(), user.Email!, user.DisplayName, user.EmailConfirmed, roles.ToList());
    }
}
