using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Domain.Enums;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface IArchiveService
{
    Task<List<ArchiveDto>> ListAsync(long? categoryId, string? status);
    Task<ArchiveDto?> GetAsync(long id);
    Task<ArchiveDto> CreateAsync(CreateArchiveDto dto, string? creatorName = null);
    Task<ArchiveDto?> UpdateAsync(long id, CreateArchiveDto dto);
    Task<bool> DeleteAsync(long id);

    // T13 归档审核状态机：草稿 → 待审核 → 已归档 / 退回
    Task<ArchiveDto?> SubmitAsync(long id, string? operatorName);
    Task<ArchiveDto?> AuditAsync(long id, string action, string? comment, string? operatorName);

    // 三级树（档案→文件→附件）与批量导入
    Task<ArchiveTreeDto?> GetTreeAsync(long id);
    Task<BatchImportResult> BatchImportAsync(BatchImportRequest req, string? operatorName);
}

public class ArchiveService : IArchiveService
{
    private readonly IFreeSql _db;
    public ArchiveService(IFreeSql db) => _db = db;

    public async Task<List<ArchiveDto>> ListAsync(long? categoryId, string? status)
    {
        var q = _db.Select<Archive>();
        if (categoryId.HasValue) q = q.Where(a => a.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ArchiveStatus>(status, out var st))
            q = q.Where(a => a.Status == st);
        var list = await q.OrderByDescending(a => a.Id).ToListAsync();
        return list.Select(ToDto).ToList()!;
    }

    public async Task<ArchiveDto?> GetAsync(long id)
        => ToDto(await _db.Select<Archive>().Where(a => a.Id == id).FirstAsync());

    public async Task<ArchiveDto> CreateAsync(CreateArchiveDto dto, string? creatorName = null)
    {
        var cat = await _db.Select<ArchiveCategory>().Where(c => c.Id == dto.CategoryId).FirstAsync()
                  ?? throw new InvalidOperationException("门类不存在");

        long? creatorId = null;
        if (!string.IsNullOrWhiteSpace(creatorName))
        {
            var u = await _db.Select<SysUser>().Where(x => x.Username == creatorName).FirstAsync();
            creatorId = u?.Id;
        }

        var year = dto.Year ?? DateTime.UtcNow.Year;
        var entity = new Archive
        {
            CategoryId = dto.CategoryId,
            ParentId = dto.ParentId,
            IsProject = dto.IsProject,
            Title = dto.Title,
            Year = year,
            Retention = dto.Retention ?? cat.DefaultRetention,
            SecrecyLevel = dto.SecrecyLevel ?? "公开",
            OrgUnit = dto.OrgUnit,
            DynamicFields = dto.DynamicFields,
            Status = ArchiveStatus.Draft,
            CreatorId = creatorId,
            ArchiveNo = await GenerateNoAsync(cat.Id, cat.FondsId, cat.Code, year, cat.FkRule)
        };
        entity.Id = await _db.Insert(entity).ExecuteIdentityAsync();
        return ToDto(entity)!;
    }

    public async Task<ArchiveDto?> UpdateAsync(long id, CreateArchiveDto dto)
    {
        var e = await _db.Select<Archive>().Where(a => a.Id == id).FirstAsync();
        if (e == null) return null;
        e.Title = dto.Title;
        e.ParentId = dto.ParentId;
        e.IsProject = dto.IsProject;
        e.Year = dto.Year;
        e.Retention = dto.Retention;
        e.SecrecyLevel = dto.SecrecyLevel;
        e.OrgUnit = dto.OrgUnit;
        e.DynamicFields = dto.DynamicFields;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.Update<Archive>().SetSource(e).ExecuteAffrowsAsync();
        return ToDto(e);
    }

    public async Task<bool> DeleteAsync(long id)
        => await _db.Delete<Archive>().Where(a => a.Id == id).ExecuteAffrowsAsync() > 0;

    /// <summary>T13：提交归档（草稿 → 待审核）</summary>
    public async Task<ArchiveDto?> SubmitAsync(long id, string? operatorName)
    {
        var e = await _db.Select<Archive>().Where(a => a.Id == id).FirstAsync();
        if (e == null) return null;
        if (e.Status != ArchiveStatus.Draft)
            throw new InvalidOperationException("仅草稿可提交归档");
        e.Status = ArchiveStatus.Submitted;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.Update<Archive>().SetSource(e).ExecuteAffrowsAsync();
        await WriteAudit(operatorName, "archive.submit", "Archive", id, $"提交归档 #{id}");
        return ToDto(e);
    }

    /// <summary>T13：审核通过/退回（待审核 → 已归档 / 退回）</summary>
    public async Task<ArchiveDto?> AuditAsync(long id, string action, string? comment, string? operatorName)
    {
        var e = await _db.Select<Archive>().Where(a => a.Id == id).FirstAsync();
        if (e == null) return null;
        if (e.Status != ArchiveStatus.Submitted)
            throw new InvalidOperationException("仅待审核档案可审核");
        if (string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
            e.Status = ArchiveStatus.Archived;
        else if (string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
            e.Status = ArchiveStatus.Rejected;
        else
            throw new ArgumentException("action 仅支持 approve / reject");
        e.UpdatedAt = DateTime.UtcNow;
        await _db.Update<Archive>().SetSource(e).ExecuteAffrowsAsync();
        await WriteAudit(operatorName, $"archive.audit.{action}", "Archive", id,
            $"审核({action}) #{id}" + (string.IsNullOrWhiteSpace(comment) ? "" : $" 意见:{comment}"));
        return ToDto(e);
    }

    /// <summary>三级树：档案 → 文件 → 附件</summary>
    public async Task<ArchiveTreeDto?> GetTreeAsync(long id)
    {
        var a = await _db.Select<Archive>().Where(x => x.Id == id).FirstAsync();
        if (a == null) return null;
        var files = await _db.Select<FileItem>().Where(f => f.ArchiveId == id).OrderBy(f => f.SeqNo ?? 0).ToListAsync();
        var tree = new ArchiveTreeDto { Archive = ToDto(a) };
        foreach (var f in files)
        {
            var atts = await _db.Select<Attachment>().Where(x => x.FileItemId == f.Id).ToListAsync();
            tree.Files.Add(new FileTreeDto
            {
                File = new FileItemDto
                {
                    Id = f.Id, ArchiveId = f.ArchiveId, Title = f.Title, DocDate = f.DocDate,
                    Author = f.Author, Pages = f.Pages, SeqNo = f.SeqNo, FileNo = f.FileNo,
                    ProjectNo = f.ProjectNo, DynamicFields = f.DynamicFields, CreatedAt = f.CreatedAt
                },
                Attachments = atts.Select(x => new AttachmentDto
                {
                    Id = x.Id, FileItemId = x.FileItemId, FileName = x.FileName, OriginalName = x.OriginalName,
                    MimeType = x.MimeType, SizeBytes = x.SizeBytes, Hash = x.Hash,
                    Version = x.Version, WatermarkFlag = x.WatermarkFlag, UploadedAt = x.UploadedAt
                }).ToList()
            });
        }
        return tree;
    }

    /// <summary>批量导入：逐条建档，单条失败不影响其余</summary>
    public async Task<BatchImportResult> BatchImportAsync(BatchImportRequest req, string? operatorName)
    {
        var result = new BatchImportResult();
        foreach (var item in req.Items)
        {
            try
            {
                var dto = new CreateArchiveDto
                {
                    CategoryId = req.CategoryId > 0 ? req.CategoryId : item.CategoryId,
                    ParentId = item.ParentId, IsProject = item.IsProject,
                    Title = item.Title, Year = item.Year, Retention = item.Retention,
                    SecrecyLevel = item.SecrecyLevel, OrgUnit = item.OrgUnit, DynamicFields = item.DynamicFields
                };
                var created = await CreateAsync(dto, operatorName);
                result.Items.Add(created);
                result.Created++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"{item.Title}: {ex.Message}");
            }
        }
        await WriteAudit(operatorName, "archive.batch-import", "Archive", req.CategoryId,
            $"批量导入 {result.Created} 条，失败 {result.Failed} 条");
        return result;
    }

    private async Task WriteAudit(string? userName, string action, string targetType, long targetId, string detail)
    {
        long? uid = null;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var u = await _db.Select<SysUser>().Where(x => x.Username == userName).FirstAsync();
            uid = u?.Id;
        }
        await _db.Insert(new AuditLog
        {
            UserId = uid,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Detail = detail,
            Ip = null,
            Ts = DateTime.UtcNow
        }).ExecuteAffrowsAsync();
    }

    /// <summary>
    /// 档号生成：按门类 fk_rule 模板解析 {fonds}/{code}/{year}/{seq}。
    /// 同一门类+年度自增序号，事务隔离由数据库并发控制，演示级实现。
    /// </summary>
    public async Task<string> GenerateNoAsync(long categoryId, long fondsId, string code, int year, string? fkRule)
    {
        var fonds = await _db.Select<Fonds>().Where(f => f.Id == fondsId).FirstAsync();
        var fondsCode = fonds?.Code ?? "F1";
        var seq = await _db.Select<Archive>()
            .Where(a => a.CategoryId == categoryId && a.Year == year).CountAsync() + 1;
        var rule = string.IsNullOrWhiteSpace(fkRule) ? "{fonds}-{code}-{year}-{seq}" : fkRule;
        return rule
            .Replace("{fonds}", fondsCode)
            .Replace("{code}", code)
            .Replace("{year}", year.ToString())
            .Replace("{seq}", seq.ToString("D3"));
    }

    private static ArchiveDto ToDto(Archive a) => new()
    {
        Id = a.Id, CategoryId = a.CategoryId, ParentId = a.ParentId, IsProject = a.IsProject,
        ArchiveNo = a.ArchiveNo, Title = a.Title, Year = a.Year, Retention = a.Retention,
        SecrecyLevel = a.SecrecyLevel, OrgUnit = a.OrgUnit, DynamicFields = a.DynamicFields,
        Status = a.Status.ToString()
    };
}
