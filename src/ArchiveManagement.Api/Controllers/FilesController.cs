using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileItemService _fileSvc;
    private readonly IAttachmentService _attSvc;
    public FilesController(IFileItemService fileSvc, IAttachmentService attSvc)
        => (_fileSvc, _attSvc) = (fileSvc, attSvc);

    // ===== 文件（件级）=====
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long archiveId)
        => Ok(new { code = 0, data = await _fileSvc.ListByArchiveAsync(archiveId) });

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) => Ok(new { code = 0, data = await _fileSvc.GetAsync(id) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFileItemDto dto)
        => Ok(new { code = 0, data = await _fileSvc.CreateAsync(dto) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] CreateFileItemDto dto)
        => Ok(new { code = 0, data = await _fileSvc.UpdateAsync(id, dto) });

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id) => Ok(new { code = 0, data = await _fileSvc.DeleteAsync(id) });

    // ===== 附件（电子文件级）：嵌套在文件下 =====
    // 列出某文件下的所有附件：GET /api/files/{fileId}/attachments
    [HttpGet("{fileId:long}/attachments")]
    public async Task<IActionResult> ListAttachments(long fileId)
        => Ok(new { code = 0, data = await _attSvc.ListByFileAsync(fileId) });

    // 上传附件（multipart）：POST /api/files/{fileId}/attachments
    [HttpPost("{fileId:long}/attachments")]
    [RequestSizeLimit(200_000_000)] // 演示上限 200MB
    public async Task<IActionResult> UploadAttachment(long fileId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { code = 400, msg = "未收到文件" });
        await using var stream = file.OpenReadStream();
        var result = await _attSvc.UploadAsync(fileId, file.FileName, file.ContentType, file.Length, stream, null);
        return Ok(new { code = 0, data = result });
    }
}
