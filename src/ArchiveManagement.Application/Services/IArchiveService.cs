using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Domain.Enums;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface IArchiveService
{
    Task<List<ArchiveDto>> ListAsync(long? categoryId, string? status, string? username = null);
    Task<PagedArchiveResult> ListPagedAsync(long? categoryId, string? status, int page, int pageSize, string? username = null);
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

    // 门类级结构树：项目 → 案卷 → 卷内文件（基建类用；无项目的门类退化为 案卷 → 文件）
    Task<List<ProjectTreeNode>> ProjectTreeAsync(long categoryId, string? username = null);

    // Excel 批量导入：首个工作表，表头按中文列名/门类元数据字段映射
    Task<BatchImportResult> ImportExcelAsync(Stream stream, long categoryId, string? operatorName);

    // 供导入模板下载取门类（含模板与默认保管期限）
    Task<ArchiveCategory?> GetCategoryForImportAsync(long categoryId);
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

    /// <summary>分页列表：密级过滤在内存完成（五维权限依赖密级比较），故先全量取再用 Skip/Take</summary>
    public async Task<PagedArchiveResult> ListPagedAsync(long? categoryId, string? status,
        int page, int pageSize, string? username = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : (pageSize > 200 ? 200 : pageSize);

        var all = await ListAsync(categoryId, status, username);
        var total = all.Count;
        return new PagedArchiveResult
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
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
    /// Excel 批量导入：解析首个工作表。识别固定列（题名/年度/保管期限/密级/归档单位/是否项目/所属项目档号），
    /// 其余列按门类元数据模板的 label 或 key 映射进 dynamicFields。
    /// </summary>
    public async Task<BatchImportResult> ImportExcelAsync(Stream stream, long categoryId, string? operatorName)
    {
        var cat = await _db.Select<ArchiveCategory>().Where(c => c.Id == categoryId).FirstAsync();
        if (cat == null) throw new ArgumentException("门类不存在");

        var tplFields = ParseTemplateFields(cat.TemplateJson);
        var items = new List<CreateArchiveDto>();
        var errors = new List<string>();

        using var wb = new ClosedXML.Excel.XLWorkbook(stream);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws == null)
        {
            errors.Add("工作簿中没有工作表");
            return new BatchImportResult { Errors = errors };
        }

        var firstRow = ws.FirstRowUsed();
        var lastRow = ws.LastRowUsed();
        if (firstRow == null || lastRow == null || lastRow.RowNumber() <= firstRow.RowNumber())
        {
            errors.Add("工作表为空或仅有表头");
            return new BatchImportResult { Errors = errors };
        }

        // 表头 → 字段标识
        var colMap = new Dictionary<int, string>();
        foreach (var cell in firstRow.CellsUsed())
        {
            var text = Norm(cell.GetString());
            if (string.IsNullOrEmpty(text)) continue;
            var mapped = MapHeader(text, tplFields);
            if (!string.IsNullOrEmpty(mapped)) colMap[cell.Address.ColumnNumber] = mapped;
        }
        if (!colMap.Values.Contains("title"))
        {
            errors.Add("未识别到『题名』列，请检查表头（支持：题名/标题/名称/title）");
            return new BatchImportResult { Errors = errors };
        }

        var defYear = DateTime.UtcNow.Year;
        for (int rowIdx = firstRow.RowNumber() + 1; rowIdx <= lastRow.RowNumber(); rowIdx++)
        {
            var row = ws.Row(rowIdx);
            if (row.CellsUsed().Count() == 0) continue; // 空行

            var dto = new CreateArchiveDto { CategoryId = categoryId, Year = defYear };
            var dyn = new Dictionary<string, object?>();
            string? parentNo = null;

            foreach (var kv in colMap)
            {
                var val = row.Cell(kv.Key).GetString().Trim();
                if (string.IsNullOrEmpty(val)) continue;
                var tag = kv.Value;
                if (tag.StartsWith("meta:"))
                {
                    dyn[tag.Substring(5)] = val;
                    continue;
                }
                switch (tag)
                {
                    case "title": dto.Title = val; break;
                    case "year": if (int.TryParse(val, out var y)) dto.Year = y; break;
                    case "retention": dto.Retention = val; break;
                    case "secrecy": dto.SecrecyLevel = val; break;
                    case "orgUnit": dto.OrgUnit = val; break;
                    case "isProject":
                        dto.IsProject = val is "是" or "Y" or "y" or "TRUE" or "true" or "1";
                        break;
                    case "parentNo": parentNo = val; break;
                }
            }

            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                errors.Add($"第 {rowIdx} 行：题名为空，已跳过");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(parentNo))
            {
                var parent = await _db.Select<Archive>()
                    .Where(a => a.ArchiveNo == parentNo && a.CategoryId == categoryId && !a.IsDeleted)
                    .FirstAsync();
                if (parent == null)
                {
                    errors.Add($"第 {rowIdx} 行：所属项目档号『{parentNo}』不存在，已按散卷导入");
                }
                else dto.ParentId = parent.Id;
            }
            dto.DynamicFields = dyn.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(dyn)
                : "{}";
            items.Add(dto);
        }

        if (items.Count == 0)
            return new BatchImportResult { Created = 0, Failed = errors.Count, Errors = errors };

        var r = await BatchImportAsync(new BatchImportRequest { CategoryId = categoryId, Items = items }, operatorName);
        r.Failed += errors.Count;
        r.Errors.InsertRange(0, errors);
        return r;
    }

    public async Task<ArchiveCategory?> GetCategoryForImportAsync(long categoryId)
        => await _db.Select<ArchiveCategory>().Where(c => c.Id == categoryId).FirstAsync();

    private static string Norm(string s) => s.Replace(" ", "").Replace("　", "").Replace("*", "").Trim();

    private static string MapHeader(string text, List<(string Key, string Label)> tplFields)
    {
        var t = text.ToLowerInvariant();
        // 固定列
        if (t is "题名" or "标题" or "名称" or "案卷题名" or "title") return "title";
        if (t is "年度" or "年份" or "归档年度" or "year") return "year";
        if (t is "保管期限" or "期限" or "retention") return "retention";
        if (t is "密级" or "秘密等级" or "保密等级" or "secrecylevel") return "secrecy";
        if (t is "归档单位" or "部门" or "责任者" or "形成单位" or "orgunit") return "orgUnit";
        if (t is "是否项目" or "项目" or "isproject") return "isProject";
        if (t is "所属项目档号" or "父档号" or "项目档号" or "parentno") return "parentNo";

        // 门类模板字段：先按 label 再按 key
        foreach (var (key, label) in tplFields)
            if (!string.IsNullOrEmpty(label) && Norm(label).Equals(text, StringComparison.OrdinalIgnoreCase))
                return "meta:" + key;
        foreach (var (key, _) in tplFields)
            if (key.Equals(text, StringComparison.OrdinalIgnoreCase))
                return "meta:" + key;
        return string.Empty;
    }

    private static List<(string Key, string Label)> ParseTemplateFields(string? json)
    {
        var list = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var key = el.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                    var label = el.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(key)) list.Add((key, label));
                }
            }
        }
        catch { /* 模板非法则忽略动态列映射 */ }
        return list;
    }

    /// <summary>
    /// 门类级结构树：项目(IsProject=true 或 有子案卷) → 案卷 → 卷内文件。
    /// 无项目概念的门类退化为「案卷 → 卷内文件」两层。
    /// </summary>
    public async Task<List<ProjectTreeNode>> ProjectTreeAsync(long categoryId, string? username = null)
    {
        var p = await _perm.ResolveAsync(username);
        var archives = (await _db.Select<Archive>()
                .Where(a => a.CategoryId == categoryId && !a.IsDeleted)
                .OrderBy(a => a.ArchiveNo)
                .ToListAsync())
            .Where(a => _perm.CanSee(p, a.CategoryId, a.SecrecyLevel))
            .ToList();

        if (archives.Count == 0) return new List<ProjectTreeNode>();

        var ids = archives.Select(a => a.Id).ToList();
        var fileCounts = await _db.Select<FileItem>()
            .Where(f => ids.Contains(f.ArchiveId) && !f.IsDeleted)
            .GroupBy(f => f.ArchiveId)
            .ToListAsync(g => new { ArchiveId = g.Key, Cnt = g.Count() });
        var countMap = fileCounts.ToDictionary(x => x.ArchiveId, x => x.Cnt);

        // 卷内文件（仅一层，避免大数据量全展开）
        var files = await _db.Select<FileItem>()
            .Where(f => ids.Contains(f.ArchiveId) && !f.IsDeleted)
            .OrderBy(f => f.SeqNo)
            .ToListAsync();

        ProjectTreeNode Node(Archive a, string type) => new()
        {
            Id = a.Id,
            Label = string.IsNullOrWhiteSpace(a.ArchiveNo) ? a.Title : $"{a.ArchiveNo} {a.Title}",
            Type = type,
            ArchiveNo = a.ArchiveNo,
            Status = a.Status.ToString(),
            FileCount = countMap.TryGetValue(a.Id, out var c) ? c : 0
        };

        var byId = archives.ToDictionary(a => a.Id);
        var childIds = archives.Where(a => a.ParentId.HasValue && byId.ContainsKey(a.ParentId.Value))
                               .Select(a => a.ParentId!.Value).ToHashSet();
        // 项目：显式标记 IsProject，或被别的案卷认作父节点
        var isProject = archives.Where(a => a.IsProject || childIds.Contains(a.Id)).ToHashSet();

        var roots = new List<ProjectTreeNode>();
        foreach (var a in archives)
        {
            var isChild = a.ParentId.HasValue && byId.ContainsKey(a.ParentId.Value);
            if (isChild) continue; // 子节点挂到父节点下
            var node = Node(a, isProject.Contains(a) ? "project" : "archive");
            node.Children = BuildChildren(a.Id);
            roots.Add(node);
        }

        List<ProjectTreeNode> BuildChildren(long parentArchiveId)
        {
            var list = new List<ProjectTreeNode>();
            foreach (var c in archives.Where(x => x.ParentId == parentArchiveId))
            {
                var cn = Node(c, "archive");
                cn.Children = files.Where(f => f.ArchiveId == c.Id).Select(f => new ProjectTreeNode
                {
                    Id = f.Id,
                    Label = $"{f.SeqNo}. {f.Title}",
                    Type = "file",
                    Status = null
                }).ToList();
                list.Add(cn);
            }
            // 项目自身若也挂着件（未走子案卷），一并列出
            if (list.Count == 0)
            {
                list.AddRange(files.Where(f => f.ArchiveId == parentArchiveId).Select(f => new ProjectTreeNode
                {
                    Id = f.Id,
                    Label = $"{f.SeqNo}. {f.Title}",
                    Type = "file"
                }));
            }
            return list;
        }

        return roots;
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
