using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/appraisal")]
[Authorize]
public class AppraisalController : ControllerBase
{
    private readonly IAppraisalService _svc;
    public AppraisalController(IAppraisalService svc) => _svc = svc;

    /// <summary>到期保管期限清单；带 page/pageSize 时返回分页结构，否则返回数组（向后兼容）</summary>
    [HttpGet("due")]
    public async Task<IActionResult> Due(
        [FromQuery] bool excludeProposed = false,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        if (page.HasValue || pageSize.HasValue)
            return Ok(new { code = 0, data = await _svc.ListDuePagedAsync(page ?? 1, pageSize ?? 20, excludeProposed) });
        return Ok(new { code = 0, data = await _svc.ListDueAsync(excludeProposed) });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalDto dto)
        => Ok(new { code = 0, data = await _svc.CreateAsync(dto, User.Identity?.Name) });

    /// <summary>鉴定任务列表；带 page/pageSize 时返回分页结构，否则返回数组（向后兼容）</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        if (page.HasValue || pageSize.HasValue)
            return Ok(new { code = 0, data = await _svc.ListPagedAsync(page ?? 1, pageSize ?? 20, status) });
        return Ok(new { code = 0, data = await _svc.ListAsync() });
    }

    [HttpPut("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] AppraisalAuditDto dto)
        => Ok(new { code = 0, data = await _svc.ApproveAsync(id, dto.Action, User.Identity?.Name) });

    [HttpPut("{id:long}/execute")]
    public async Task<IActionResult> Execute(long id)
        => Ok(new { code = 0, data = await _svc.ExecuteAsync(id, User.Identity?.Name) });
}
