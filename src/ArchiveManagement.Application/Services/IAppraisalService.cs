using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Domain.Enums;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface IAppraisalService
{
    Task<List<DueArchiveDto>> ListDueAsync(bool excludeProposed = false);
    Task<PagedDueResult> ListDuePagedAsync(int page, int pageSize, bool excludeProposed = false);
    Task<AppraisalDto> CreateAsync(CreateAppraisalDto dto, string? operatorName);
    Task<List<AppraisalDto>> ListAsync();
    Task<PagedAppraisalResult> ListPagedAsync(int page, int pageSize, string? status = null);
    Task<AppraisalDto?> ApproveAsync(long id, string action, string? operatorName);
    Task<AppraisalDto?> ExecuteAsync(long id, string? operatorName);
}

public class AppraisalService : IAppraisalService
{
    private readonly IFreeSql _db;
    public AppraisalService(IFreeSql db) => _db = db;

    /// <summary>到期保管期限清单：retention 为 N年 且 (年度+N) &lt;= 当前年；"永久"不过期</summary>
    public Task<List<DueArchiveDto>> ListDueAsync(bool excludeProposed = false) => BuildDueListAsync(excludeProposed);

    public async Task<PagedDueResult> ListDuePagedAsync(int page, int pageSize, bool excludeProposed = false)
    {
        var all = await BuildDueListAsync(excludeProposed);
        return Paging(all, page, pageSize, items => new PagedDueResult { Items = items });
    }

    private async Task<List<DueArchiveDto>> BuildDueListAsync(bool excludeProposed)
    {
        var curYear = DateTime.UtcNow.Year;
        // 已逻辑删除 / 已销毁的档案不再提示鉴定
        var archives = await _db.Select<Archive>()
            .Where(a => !a.IsDeleted && a.Status != ArchiveStatus.Destroyed)
            .ToListAsync();
        var result = new List<DueArchiveDto>();
        foreach (var a in archives)
        {
            var years = ParseRetentionYears(a.Retention);
            if (years <= 0) continue; // 永久或无法解析
            var baseYear = a.Year ?? curYear;
            var expiry = baseYear + years;
            if (expiry > curYear) continue;
            result.Add(new DueArchiveDto { Archive = ToArchiveDto(a), ExpiryYear = expiry });
        }

        // 标注在途的鉴定任务，便于前端区分"已提议/未提议"
        if (result.Count > 0)
        {
            var ids = result.Select(x => x.Archive.Id).Distinct().ToList();
            var inFlight = await _db.Select<Appraisal>()
                .Where(x => ids.Contains(x.TargetId) && x.TargetType == "archive"
                            && (x.Status == AppraisalStatus.Pending || x.Status == AppraisalStatus.Approved))
                .ToListAsync();
            foreach (var d in result)
            {
                var hit = inFlight.FirstOrDefault(x => x.TargetId == d.Archive.Id);
                if (hit != null)
                {
                    d.AppraisalId = hit.Id;
                    d.AppraisalStatus = hit.Status.ToString();
                }
            }
            if (excludeProposed)
                result = result.Where(x => x.AppraisalId == null).ToList();
        }

        return result.OrderBy(x => x.ExpiryYear).ThenBy(x => x.Archive.ArchiveNo).ToList();
    }

    private static T Paging<T>(List<DueArchiveDto> all, int page, int pageSize, Func<List<DueArchiveDto>, T> build) where T : PagedDueResult
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : (pageSize > 200 ? 200 : pageSize);
        var total = all.Count;
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var r = build(items);
        r.Total = total;
        r.Page = page;
        r.PageSize = pageSize;
        r.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return r;
    }

    public async Task<AppraisalDto> CreateAsync(CreateAppraisalDto dto, string? operatorName)
    {
        var proposerId = await ResolveUserId(operatorName);
        var e = new Appraisal
        {
            TargetType = dto.TargetType,
            TargetId = dto.TargetId,
            ProposeReason = dto.ProposeReason,
            Status = AppraisalStatus.Pending
        };
        e.Id = await _db.Insert(e).ExecuteIdentityAsync();
        return await ToDto(e, proposerId);
    }

    public async Task<List<AppraisalDto>> ListAsync()
    {
        var list = await _db.Select<Appraisal>().OrderByDescending(x => x.Id).ToListAsync();
        var dtos = new List<AppraisalDto>();
        foreach (var a in list) dtos.Add(await ToDto(a, null));
        return dtos;
    }

    public async Task<PagedAppraisalResult> ListPagedAsync(int page, int pageSize, string? status = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : (pageSize > 200 ? 200 : pageSize);

        var q = _db.Select<Appraisal>();
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<AppraisalStatus>(status, true, out var st))
            q = q.Where(x => x.Status == st);

        var total = (int)await q.CountAsync();
        var list = await q.OrderByDescending(x => x.Id)
            .Page(page, pageSize).ToListAsync();

        var items = new List<AppraisalDto>();
        foreach (var a in list) items.Add(await ToDto(a, null));

        return new PagedAppraisalResult
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Items = items
        };
    }

    public async Task<AppraisalDto?> ApproveAsync(long id, string action, string? operatorName)
    {
        var e = await _db.Select<Appraisal>().Where(x => x.Id == id).FirstAsync();
        if (e == null) return null;
        if (e.Status != AppraisalStatus.Pending)
            throw new InvalidOperationException("仅待审批鉴定可处理");
        var approverId = await ResolveUserId(operatorName);
        if (string.Equals(action, "keep", StringComparison.OrdinalIgnoreCase))
            e.Status = AppraisalStatus.Kept;
        else if (string.Equals(action, "destroy", StringComparison.OrdinalIgnoreCase))
            e.Status = AppraisalStatus.Approved; // 待执行销毁
        else
            throw new ArgumentException("action 仅支持 keep / destroy");
        e.ApprovedBy = approverId;
        await _db.Update<Appraisal>().SetSource(e).ExecuteAffrowsAsync();
        await WriteAudit(operatorName, $"appraisal.approve.{action}", "Appraisal", id, $"鉴定审批({action}) #{id}");
        return await ToDto(e, approverId);
    }

    public async Task<AppraisalDto?> ExecuteAsync(long id, string? operatorName)
    {
        var e = await _db.Select<Appraisal>().Where(x => x.Id == id).FirstAsync();
        if (e == null) return null;
        if (e.Status != AppraisalStatus.Approved)
            throw new InvalidOperationException("仅已审批(销毁)的鉴定可被执行");

        // 执行销毁：目标档案置"已销毁"
        if (e.TargetType == "archive")
        {
            var a = await _db.Select<Archive>().Where(x => x.Id == e.TargetId).FirstAsync();
            if (a != null)
            {
                a.Status = ArchiveStatus.Destroyed;
                a.UpdatedAt = DateTime.UtcNow;
                await _db.Update<Archive>().SetSource(a).ExecuteAffrowsAsync();
            }
        }
        e.Status = AppraisalStatus.Destroyed;
        e.ExecutedAt = DateTime.UtcNow;
        await _db.Update<Appraisal>().SetSource(e).ExecuteAffrowsAsync();
        await WriteAudit(operatorName, "appraisal.execute", "Appraisal", id, $"执行销毁 #{id}");
        return await ToDto(e, null);
    }

    private async Task<long?> ResolveUserId(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        var u = await _db.Select<SysUser>().Where(x => x.Username == username).FirstAsync();
        return u?.Id;
    }

    private async Task WriteAudit(string? userName, string action, string targetType, long targetId, string detail)
    {
        long? uid = await ResolveUserId(userName);
        await _db.Insert(new AuditLog
        {
            UserId = uid, Action = action, TargetType = targetType,
            TargetId = targetId, Detail = detail, Ts = DateTime.UtcNow
        }).ExecuteAffrowsAsync();
    }

    private static ArchiveDto ToArchiveDto(Archive a) => new()
    {
        Id = a.Id, CategoryId = a.CategoryId, ParentId = a.ParentId, IsProject = a.IsProject,
        ArchiveNo = a.ArchiveNo, Title = a.Title, Year = a.Year, Retention = a.Retention,
        SecrecyLevel = a.SecrecyLevel, OrgUnit = a.OrgUnit, DynamicFields = a.DynamicFields,
        Status = a.Status.ToString()
    };

    private async Task<AppraisalDto> ToDto(Appraisal a, long? approverId)
    {
        var approver = approverId.HasValue
            ? await _db.Select<SysUser>().Where(u => u.Id == approverId.Value).FirstAsync()
            : (a.ApprovedBy.HasValue ? await _db.Select<SysUser>().Where(u => u.Id == a.ApprovedBy.Value).FirstAsync() : null);
        string targetTitle = string.Empty;
        if (a.TargetType == "archive")
        {
            var ar = await _db.Select<Archive>().Where(x => x.Id == a.TargetId).FirstAsync();
            targetTitle = ar?.Title ?? $"档案#{a.TargetId}";
        }
        else
        {
            var f = await _db.Select<FileItem>().Where(x => x.Id == a.TargetId).FirstAsync();
            targetTitle = f?.Title ?? $"文件#{a.TargetId}";
        }
        return new AppraisalDto
        {
            Id = a.Id, TargetType = a.TargetType, TargetId = a.TargetId, TargetTitle = targetTitle,
            ProposeReason = a.ProposeReason, Status = a.Status,
            ApprovedById = a.ApprovedBy, ApprovedByName = approver?.Name ?? approver?.Username ?? "",
            ExecutedAt = a.ExecutedAt, CreatedAt = a.CreatedAt
        };
    }

    private static int ParseRetentionYears(string? retention)
    {
        if (string.IsNullOrWhiteSpace(retention)) return 0;
        if (retention.Contains("永久")) return -1; // 标记"永不"
        var m = System.Text.RegularExpressions.Regex.Match(retention, @"(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }
}
