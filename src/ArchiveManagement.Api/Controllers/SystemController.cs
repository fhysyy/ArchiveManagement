using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/system")]
[Authorize]
public class SystemController : ControllerBase
{
    private readonly ISystemService _svc;
    public SystemController(ISystemService svc) => _svc = svc;

    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> Health() => Ok(new { code = 0, data = await _svc.HealthAsync() });

    [HttpPost("backup")]
    public async Task<IActionResult> Backup() => Ok(new { code = 0, data = await _svc.BackupAsync(User.Identity?.Name) });
}

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ISystemService _svc;
    public UsersController(ISystemService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List() => Ok(new { code = 0, data = await _svc.ListUsersAsync() });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        => Ok(new { code = 0, data = await _svc.CreateUserAsync(dto) });

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id) => Ok(new { code = 0, data = await _svc.DeleteUserAsync(id) });

    [HttpPut("{id:long}/password")]
    public async Task<IActionResult> ChangePassword(long id, [FromBody] ChangePasswordDto dto)
        => Ok(new { code = 0, data = await _svc.ChangePasswordAsync(id, dto.NewPassword) });

    /// <summary>启用(1) / 禁用(0)</summary>
    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, [FromBody] SetUserStatusDto dto)
        => Ok(new { code = 0, data = await _svc.SetUserStatusAsync(id, dto.Status) });
}

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly ISystemService _svc;
    public RolesController(ISystemService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List() => Ok(new { code = 0, data = await _svc.ListRolesAsync() });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
        => Ok(new { code = 0, data = await _svc.CreateRoleAsync(dto) });

    // 五维权限：角色 × 门类 × 操作 × 密级
    [HttpGet("{id:long}/permissions")]
    public async Task<IActionResult> GetPermissions(long id)
        => Ok(new { code = 0, data = await _svc.ListPermissionsAsync(id) });

    [HttpPut("{id:long}/permissions")]
    public async Task<IActionResult> SetPermissions(long id, [FromBody] SetPermissionsRequest req)
        => Ok(new { code = 0, data = await _svc.SetPermissionsAsync(id, req.Items) });

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id) => Ok(new { code = 0, data = await _svc.DeleteRoleAsync(id) });
}
