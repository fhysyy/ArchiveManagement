using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _svc;
    public CategoriesController(ICategoryService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List() => Ok(new { code = 0, data = await _svc.ListAsync() });

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) => Ok(new { code = 0, data = await _svc.GetAsync(id) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto)
        => Ok(new { code = 0, data = await _svc.CreateAsync(dto) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] CreateCategoryDto dto)
        => Ok(new { code = 0, data = await _svc.UpdateAsync(id, dto) });

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id) => Ok(new { code = 0, data = await _svc.DeleteAsync(id) });
}
