using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IConfiguration _config;
    public AuthController(IAuthService auth, IConfiguration config) { _auth = auth; _config = config; }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var (ok, result) = await _auth.LoginAsync(dto);
        if (!ok) return Unauthorized(new { code = 401, msg = "用户名或密码错误" });

        var key = _config["Jwt:Key"] ?? "dev-secret-key-change-me-which-is-long-enough";
        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                claims: new[]
                {
                    new Claim(ClaimTypes.Name, result!.Username),
                    new Claim(ClaimTypes.Role, result.Role)
                },
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    SecurityAlgorithms.HmacSha256)));

        result.Token = token;
        return Ok(new { code = 0, msg = "ok", data = result });
    }
}
