using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchService _svc;
    public SearchController(ISearchService svc) => _svc = svc;

    [HttpPost]
    public async Task<IActionResult> Search([FromBody] SearchArchiveDto dto)
        => Ok(new { code = 0, data = await _svc.SearchAsync(dto) });

    // 检索联想
    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string q)
        => Ok(new { code = 0, data = await _svc.SuggestAsync(q ?? string.Empty) });
}
