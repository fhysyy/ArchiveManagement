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

    /// <summary>新增或更新全宗（仅管理员）</summary>
    [HttpPut]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Save([FromBody] SaveFondsDto dto)
        => Ok(new { code = 0, data = await _svc.SaveAsync(dto, User.Identity?.Name) });
}
