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

    [HttpGet("due")]
    public async Task<IActionResult> Due() => Ok(new { code = 0, data = await _svc.ListDueAsync() });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalDto dto)
        => Ok(new { code = 0, data = await _svc.CreateAsync(dto, User.Identity?.Name) });

    [HttpGet]
    public async Task<IActionResult> List() => Ok(new { code = 0, data = await _svc.ListAsync() });

    [HttpPut("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] AppraisalAuditDto dto)
        => Ok(new { code = 0, data = await _svc.ApproveAsync(id, dto.Action, User.Identity?.Name) });

    [HttpPut("{id:long}/execute")]
    public async Task<IActionResult> Execute(long id)
        => Ok(new { code = 0, data = await _svc.ExecuteAsync(id, User.Identity?.Name) });
}
