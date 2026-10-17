using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/archives")]
[Authorize]
public class ArchivesController : ControllerBase
{
    private readonly IArchiveService _svc;
    public ArchivesController(IArchiveService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? categoryId, [FromQuery] string? status)
        => Ok(new { code = 0, data = await _svc.ListAsync(categoryId, status, User.Identity?.Name) });

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
        => Ok(new { code = 0, data = await _svc.GetAsync(id, User.Identity?.Name) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateArchiveDto dto)
        => Ok(new { code = 0, data = await _svc.CreateAsync(dto, User.Identity?.Name) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] CreateArchiveDto dto)
        => Ok(new { code = 0, data = await _svc.UpdateAsync(id, dto, User.Identity?.Name) });

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
        => Ok(new { code = 0, data = await _svc.DeleteAsync(id, User.Identity?.Name) });

    // T13 归档审核状态机
    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id)
        => Ok(new { code = 0, data = await _svc.SubmitAsync(id, User.Identity?.Name) });

    [HttpPost("{id:long}/audit")]
    public async Task<IActionResult> Audit(long id, [FromBody] ArchiveAuditDto dto)
        => Ok(new { code = 0, data = await _svc.AuditAsync(id, dto.Action, dto.Comment, User.Identity?.Name) });

    // 三级树：档案 → 文件 → 附件
    [HttpGet("{id:long}/tree")]
    public async Task<IActionResult> Tree(long id)
        => Ok(new { code = 0, data = await _svc.GetTreeAsync(id, User.Identity?.Name) });

    [HttpPost("batch-import")]
    public async Task<IActionResult> BatchImport([FromBody] BatchImportRequest req)
        => Ok(new { code = 0, data = await _svc.BatchImportAsync(req, User.Identity?.Name) });
}
