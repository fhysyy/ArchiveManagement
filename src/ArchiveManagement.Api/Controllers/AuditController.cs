using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditService _svc;
    public AuditController(IAuditService svc) => _svc = svc;

    /// <summary>审计日志；带 page/pageSize 时返回分页结构，否则返回最近 200 条数组（向后兼容）</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        if (page.HasValue || pageSize.HasValue)
            return Ok(new { code = 0, data = await _svc.ListPagedAsync(from, to, page ?? 1, pageSize ?? 20, action) });
        return Ok(new { code = 0, data = await _svc.ListAsync(from, to) });
    }
}
