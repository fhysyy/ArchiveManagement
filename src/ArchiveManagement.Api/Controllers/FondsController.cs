using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/fonds")]
[Authorize]
public class FondsController : ControllerBase
{
    private readonly IFondsService _svc;
    public FondsController(IFondsService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List() => Ok(new { code = 0, data = await _svc.ListAsync() });

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) => Ok(new { code = 0, data = await _svc.GetAsync(id) });

    /// <summary>新增或更新全宗（仅系统管理员；注意角色编码取自 sys_role.code = SYS_ADMIN）</summary>
    [HttpPut]
    [Authorize(Roles = "SYS_ADMIN")]
    public async Task<IActionResult> Save([FromBody] SaveFondsDto dto)
        => Ok(new { code = 0, data = await _svc.SaveAsync(dto, User.Identity?.Name) });
}
