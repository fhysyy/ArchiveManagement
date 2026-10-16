using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly IStatsService _svc;
    public StatsController(IStatsService svc) => _svc = svc;

    [HttpGet("overview")]
    public async Task<IActionResult> Overview() => Ok(new { code = 0, data = await _svc.OverviewAsync() });

    [HttpGet("borrow")]
    public async Task<IActionResult> Borrow() => Ok(new { code = 0, data = await _svc.BorrowAsync() });

    [HttpGet("due-warning")]
    public async Task<IActionResult> DueWarning() => Ok(new { code = 0, data = await _svc.DueWarningAsync() });
}
