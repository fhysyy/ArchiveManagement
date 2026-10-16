using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveManagement.Api.Controllers;

[ApiController]
[Route("api/attachments")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _svc;
    public AttachmentsController(IAttachmentService svc) => _svc = svc;

    // 下载附件：GET /api/attachments/{id}/download
    [HttpGet("{id:long}/download")]
    public async Task<IActionResult> Download(long id)
    {
        var (stream, meta) = await _svc.DownloadAsync(id);
        if (stream == null || meta == null) return NotFound(new { code = 404, msg = "附件不存在" });
        return File(stream, meta.MimeType ?? "application/octet-stream", meta.OriginalName ?? "download");
    }

    // 在线预览：inline 方式返回，浏览器能直接渲染的类型（pdf/图片/文本）
    [HttpGet("{id:long}/preview")]
    public async Task<IActionResult> Preview(long id)
    {
        var (stream, meta) = await _svc.DownloadAsync(id);
        if (stream == null || meta == null) return NotFound(new { code = 404, msg = "附件不存在" });
        var ct = meta.MimeType ?? "application/octet-stream";
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{meta.OriginalName ?? "preview"}\"");
        return File(stream, ct);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id) => Ok(new { code = 0, data = await _svc.DeleteAsync(id) });
}
