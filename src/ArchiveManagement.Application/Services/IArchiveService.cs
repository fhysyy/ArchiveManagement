using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Domain.Enums;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface IArchiveService
{
    Task<List<ArchiveDto>> ListAsync(long? categoryId, string? status, string? username = null);
    Task<ArchiveDto?> GetAsync(long id, string? username = null);
    Task<ArchiveDto> CreateAsync(CreateArchiveDto dto, string? creatorName = null);
    Task<ArchiveDto?> UpdateAsync(long id, CreateArchiveDto dto, string? username = null);
    Task<bool> DeleteAsync(long id, string? username = null);

    // T13 归档审核状态机：草稿 → 待审核 → 已归档 / 退回
    Task<ArchiveDto?> SubmitAsync(long id, string? operatorName);
    Task<ArchiveDto?> AuditAsync(long id, string action, string? comment, string? operatorName);

    // 三级树（档案→文件→附件）与批量导入
    Task<ArchiveTreeDto?> GetTreeAsync(long id, string? username = null);
    Task<BatchImportResult> BatchImportAsync(BatchImportRequest req, string? operatorName);
}

public class ArchiveService : IArchiveService
{
    private readonly IFreeSql _db;
    private readonly IPermissionService _perm;
    private readonly IAuditLogger _audit;
    private readonly IAttachmentService _att;

    public ArchiveService(IFreeSql db, IPermissionService perm, IAuditLogger audit, IAttachmentService att)
        => (_db, _perm, _audit, _att) = (db, perm, audit, att);

    // ===== 列表 / 详情：按五维权限过滤（门类 + 密级），并排除逻辑删除 =====
    public async Task<List<ArchiveDto>> ListAsync(long? categoryId, string? status, string? username = null)
    {
        var p = await _perm.ResolveAsync(username);
        var q = _db.Select<Archive>().Where(a => !a.IsDeleted);
        if (categoryId.HasValue) q = q.Where(a => a.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ArchiveStatus>(status, out var st))
            q = q.Where(a => a.Status == st);

        var list = await q.OrderByDescending(a => a.Id).ToListAsync();
        return list.Where(a => _perm.CanSee(p, a.CategoryId, a.SecrecyLevel)).Select(ToDto).ToList()!;
    }

    public async Task<ArchiveDto?> GetAsync(long id, string? username = null)
    {
        var e = await _db.Select<Archive>().Where(a => a.Id == id && !a.IsDeleted).FirstAsync();
        if (e == null) return null;
        var p = await _perm.ResolveAsync(username);
        // 无权查看一律按"不存在"返回，避免泄露档案是否存在
        if (!_perm.CanSee(p, e.CategoryId, e.SecrecyLevel)) return null;
        return ToDto(e);
    }

    public async Task<ArchiveDto> CreateAsync(CreateArchiveDto dto, string? creatorName = null)
    {
        var p = await _perm.ResolveAsync(creatorName);
        if (!_perm.Can(p, dto.CategoryId, "edit"))
            throw new UnauthorizedAccessException("当前角色无权在该门类下建档");

        var cat = await _db.Select<ArchiveCategory>().Where(c => c.Id == dto.CategoryId).FirstAsync()
                  ?? throw new InvalidOperationException("门类不存在");

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
            CreatorId = p.UserId,
            ArchiveNo = await GenerateNoAsync(cat.Id, cat.Code, year, cat.FkRule)
        };
        entity.Id = await _db.Insert(entity).ExecuteIdentityAsync();
        await _audit.LogAsync("archive.create", "Archive", entity.Id, $"新建档案 {entity.Title}");
        return ToDto(entity)!;
    }

    public async Task<ArchiveDto?> UpdateAsync(long id, CreateArchiveDto dto, string? username = null)
    {
        var e = await _db.Select<Archive>().Where(a => a.Id == id && !a.IsDeleted).FirstAsync();
        if (e == null) return null;
        var p = await _perm.ResolveAsync(username);
        if (!_perm.Can(p, e.CategoryId, "edit"))
            throw new UnauthorizedAccessException("当前角色无权修改该门类档案");
        // 状态机：仅草稿 / 退回 可编辑
        if (e.Status != ArchiveStatus.Draft && e.Status != ArchiveStatus.Rejected)
            throw new InvalidOperationException($"当前状态（{e.Status}）不可编辑，仅草稿或退回可修改");

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
        await _audit.LogAsync("archive.update", "Archive", id, $"修改档案 {e.Title}");
        return ToDto(e);
    }

    /// <summary>逻辑删除：置 IsDeleted，并级联清理其下文件与附件（含对象存储）</summary>
    public async Task<bool> DeleteAsync(long id, string? username = null)
    {
        var e = await _db.Select<Archive>().Where(a => a.Id == id && !a.IsDeleted).FirstAsync();
        if (e == null) return false;
        var p = await _perm.ResolveAsync(username);
        if (!_perm.Can(p, e.CategoryId, "destroy"))
            throw new UnauthorizedAccessException("当前角色无权删除该门类档案");

        // 级联：文件 → 附件（含对象存储清理）
        var files = await _db.Select<FileItem>().Where(f => f.ArchiveId == id && !f.IsDeleted).ToListAsync();
        foreach (var f in files)
        {
            var atts = await _db.Select<Attachment>().Where(x => x.FileItemId == f.Id).ToListAsync();
            foreach (var a in atts) await _att.DeleteAsync(a.Id);
        }
        if (files.Count > 0)
            await _db.Update<FileItem>()
                .Where(f => f.ArchiveId == id)
                .Set(f => f.IsDeleted, true).Set(f => f.DeletedAt, DateTime.UtcNow)
                .ExecuteAffrowsAsync();

        e.IsDeleted = true;
        e.DeletedAt = DateTime.UtcNow;
        var aff = await _db.Update<Archive>().SetSource(e).ExecuteAffrowsAsync();
        await _audit.LogAsync("archive.delete", "Archive", id, $"删除档案 {e.Title}（含 {files.Count} 个文件）");
        return aff > 0;
    }

    public async Task<ArchiveDto?> SubmitAsync(long id, string? operatorName)
    {
        var e = await _db.Select<Archive>().Where(a => a.Id == id && !a.IsDeleted).FirstAsync();
        if (e == null) return null;
        var p = await _perm.ResolveAsync(operatorName);
        if (!_perm.Can(p, e.CategoryId, "edit"))
            throw new UnauthorizedAccessException("当前角色无权提交该门类档案");
        if (e.Status != ArchiveStatus.Draft)
            throw new InvalidOperationException("仅草稿可提交归档");
        e.Status = ArchiveStatus.Submitted;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.Update<Archive>().SetSource(e).ExecuteAffrowsAsync();
        await _audit.LogAsync("archive.submit", "Archive", id, $"提交归档 #{id}");
        return ToDto(e);
    }

    public async Task<ArchiveDto?> AuditAsync(long id, string action, string? comment, string? operatorName)
    {
        var e = await _db.Select<Archive>().Where(a => a.Id == id && !a.IsDeleted).FirstAsync();
        if (e == null) return null;
        var p = await _perm.ResolveAsync(operatorName);
        if (!_perm.Can(p, e.CategoryId, "audit"))
            throw new UnauthorizedAccessException("当前角色无权审核该门类档案");
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
        await _audit.LogAsync($"archive.audit.{action}", "Archive", id,
            $"审核({action}) #{id}" + (string.IsNullOrWhiteSpace(comment) ? "" : $" 意见:{comment}"));
        return ToDto(e);
    }

    public async Task<ArchiveTreeDto?> GetTreeAsync(long id, string? username = null)
    {
        var a = await _db.Select<Archive>().Where(x => x.Id == id && !x.IsDeleted).FirstAsync();
        if (a == null) return null;
        var p = await _perm.ResolveAsync(username);
        if (!_perm.CanSee(p, a.CategoryId, a.SecrecyLevel)) return null;

        var files = await _db.Select<FileItem>()
            .Where(f => f.ArchiveId == id && !f.IsDeleted).OrderBy(f => f.SeqNo ?? 0).ToListAsync();
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

    public async Task<BatchImportResult> BatchImportAsync(BatchImportRequest req, string? operatorName)
    {
        var p = await _perm.ResolveAsync(operatorName);
        var result = new BatchImportResult();
        foreach (var item in req.Items)
        {
            try
            {
                var cid = req.CategoryId > 0 ? req.CategoryId : item.CategoryId;
                if (!_perm.Can(p, cid, "edit"))
                {
                    result.Failed++;
                    result.Errors.Add($"{item.Title}: 无权在该门类建档");
                    continue;
                }
                var created = await CreateAsync(new CreateArchiveDto
                {
                    CategoryId = cid, ParentId = item.ParentId, IsProject = item.IsProject,
                    Title = item.Title, Year = item.Year, Retention = item.Retention,
                    SecrecyLevel = item.SecrecyLevel, OrgUnit = item.OrgUnit, DynamicFields = item.DynamicFields
                }, operatorName);
                result.Items.Add(created);
                result.Created++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"{item.Title}: {ex.Message}");
            }
        }
        await _audit.LogAsync("archive.batch-import", "Archive", req.CategoryId,
            $"批量导入 {result.Created} 条，失败 {result.Failed} 条");
        return result;
    }

    /// <summary>
    /// 档号生成：按门类 fk_rule 解析 {fonds}/{code}/{year}/{seq}，并校验唯一性（重号则递增序号）。
    /// </summary>
    public async Task<string> GenerateNoAsync(long categoryId, string code, int year, string? fkRule)
    {
        var fonds = await _db.Select<Fonds>().FirstAsync();
        var fondsCode = fonds?.Code ?? "F1";
        var seq = await _db.Select<Archive>()
            .Where(a => a.CategoryId == categoryId && a.Year == year).CountAsync() + 1;

        var rule = string.IsNullOrWhiteSpace(fkRule) ? "{fonds}-{code}-{year}-{seq}" : fkRule;
        string Build(long s) => rule
            .Replace("{fonds}", fondsCode).Replace("{code}", code)
            .Replace("{year}", year.ToString()).Replace("{seq}", s.ToString("D3"));

        // 并发/补号场景下防重：已存在则递增，最多尝试 1000 次
        var no = Build(seq);
        for (int i = 1; i < 1000; i++)
        {
            var exists = await _db.Select<Archive>().Where(a => a.ArchiveNo == no).AnyAsync();
            if (!exists) return no;
            no = Build(seq + i);
        }
        return no;
    }

    private static ArchiveDto ToDto(Archive a) => new()
    {
        Id = a.Id, CategoryId = a.CategoryId, ParentId = a.ParentId, IsProject = a.IsProject,
        ArchiveNo = a.ArchiveNo, Title = a.Title, Year = a.Year, Retention = a.Retention,
        SecrecyLevel = a.SecrecyLevel, OrgUnit = a.OrgUnit, DynamicFields = a.DynamicFields,
        Status = a.Status.ToString()
    };
}
