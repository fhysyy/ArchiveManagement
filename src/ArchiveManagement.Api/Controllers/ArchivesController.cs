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

    /// <summary>档案列表；带 page/pageSize 时返回分页结构，否则返回数组（向后兼容）</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long? categoryId,
        [FromQuery] string? status,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        if (page.HasValue || pageSize.HasValue)
            return Ok(new { code = 0, data = await _svc.ListPagedAsync(categoryId, status, page ?? 1, pageSize ?? 20, User.Identity?.Name) });
        return Ok(new { code = 0, data = await _svc.ListAsync(categoryId, status, User.Identity?.Name) });
    }

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

    // 门类级结构树：项目 → 案卷 → 卷内文件
    [HttpGet("project-tree")]
    public async Task<IActionResult> ProjectTree([FromQuery] long categoryId)
        => Ok(new { code = 0, data = await _svc.ProjectTreeAsync(categoryId, User.Identity?.Name) });

    // 三级树：档案 → 文件 → 附件
    [HttpGet("{id:long}/tree")]
    public async Task<IActionResult> Tree(long id)
        => Ok(new { code = 0, data = await _svc.GetTreeAsync(id, User.Identity?.Name) });

    [HttpPost("batch-import")]
    public async Task<IActionResult> BatchImport([FromBody] BatchImportRequest req)
        => Ok(new { code = 0, data = await _svc.BatchImportAsync(req, User.Identity?.Name) });

    /// <summary>Excel 批量导入：form-data 提交 file + categoryId，表头按中文列名/门类元数据字段映射</summary>
    [HttpPost("import-excel")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> ImportExcel(IFormFile file, [FromForm] long categoryId)
    {
        if (file == null || file.Length == 0) return BadRequest(new { code = 400, msg = "请选择要导入的 Excel 文件" });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".xlsx" or ".xls"))
            return BadRequest(new { code = 400, msg = "仅支持 .xlsx / .xls 文件" });

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;
        var r = await _svc.ImportExcelAsync(ms, categoryId, User.Identity?.Name);
        return Ok(new { code = 0, data = r });
    }

    /// <summary>下载导入模板：按门类元数据字段生成表头</summary>
    [HttpGet("import-template")]
    public async Task<IActionResult> ImportTemplate([FromQuery] long categoryId)
    {
        var cat = await _svc.GetCategoryForImportAsync(categoryId);
        if (cat == null) return NotFound(new { code = 404, msg = "门类不存在" });

        var fields = new List<(string Key, string Label)>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(cat.TemplateJson ?? "[]");
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var key = el.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                    var label = el.TryGetProperty("label", out var l) ? l.GetString() ?? key : key;
                    if (!string.IsNullOrWhiteSpace(key)) fields.Add((key, label));
                }
        }
        catch { }

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("导入模板");
        var headers = new List<string> { "题名", "年度", "保管期限", "密级", "归档单位", "是否项目", "所属项目档号" };
        headers.AddRange(fields.Select(f => string.IsNullOrWhiteSpace(f.Label) ? f.Key : f.Label));

        for (int i = 0; i < headers.Count; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }
        // 示例行
        ws.Cell(2, 1).Value = "示例：XX工程竣工验收文件";
        ws.Cell(2, 2).Value = DateTime.Now.Year;
        ws.Cell(2, 3).Value = cat.DefaultRetention ?? "30年";
        ws.Cell(2, 4).Value = "内部";
        ws.Cell(2, 5).Value = "基建处";
        ws.Cell(2, 6).Value = "否";
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"导入模板_{cat.Name}.xlsx");
    }
}
