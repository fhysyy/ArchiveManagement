using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/borrow")]
[Authorize]
public class BorrowController : ControllerBase
{
    private readonly IBorrowService _svc;
    public BorrowController(IBorrowService svc) => _svc = svc;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBorrowDto dto)
        => Ok(new { code = 0, data = await _svc.CreateAsync(dto, User.Identity?.Name) });

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? role)
        => Ok(new { code = 0, data = await _svc.ListAsync(role ?? "applicant", User.Identity?.Name) });

    [HttpPut("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] BorrowAuditDto? dto)
        => Ok(new { code = 0, data = await _svc.ApproveAsync(id, User.Identity?.Name, dto) });

    [HttpPut("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] BorrowAuditDto? dto)
        => Ok(new { code = 0, data = await _svc.RejectAsync(id, User.Identity?.Name, dto) });

    [HttpPut("{id:long}/return")]
    public async Task<IActionResult> Return(long id)
        => Ok(new { code = 0, data = await _svc.ReturnAsync(id) });

    [HttpPost("{id:long}/renew")]
    public async Task<IActionResult> Renew(long id, [FromBody] BorrowRenewDto dto)
        => Ok(new { code = 0, data = await _svc.RenewAsync(id, dto.Days ?? 7) });
}
