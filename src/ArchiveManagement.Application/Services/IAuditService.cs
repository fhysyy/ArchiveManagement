using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface IAuditService
{
    Task<List<AuditLogDto>> ListAsync(DateTime? from, DateTime? to, int limit = 200);
    Task<PagedAuditResult> ListPagedAsync(DateTime? from, DateTime? to, int page, int pageSize, string? action = null);
}

public class AuditService : IAuditService
{
    private readonly IFreeSql _db;
    public AuditService(IFreeSql db) => _db = db;

    public async Task<List<AuditLogDto>> ListAsync(DateTime? from, DateTime? to, int limit = 200)
    {
        var q = BuildQuery(from, to, null);
        var logs = await q.OrderByDescending(a => a.Id).Limit(limit).ToListAsync();
        return await ToDtosAsync(logs);
    }

    public async Task<PagedAuditResult> ListPagedAsync(DateTime? from, DateTime? to,
        int page, int pageSize, string? action = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : (pageSize > 200 ? 200 : pageSize);

        var total = (int)await BuildQuery(from, to, action).CountAsync();
        var logs = await BuildQuery(from, to, action)
            .OrderByDescending(a => a.Id)
            .Page(page, pageSize)
            .ToListAsync();

        return new PagedAuditResult
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Items = await ToDtosAsync(logs)
        };
    }

    private ISelect<AuditLog> BuildQuery(DateTime? from, DateTime? to, string? action)
    {
        var q = _db.Select<AuditLog>();
        if (from.HasValue) q = q.Where(a => a.Ts >= from.Value);
        if (to.HasValue) q = q.Where(a => a.Ts <= to.Value);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        return q;
    }

    private async Task<List<AuditLogDto>> ToDtosAsync(List<AuditLog> logs)
    {
        // 一次性取用户表做映射，避免逐条查库
        var userIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
        var users = userIds.Count > 0
            ? await _db.Select<SysUser>().Where(u => userIds.Contains(u.Id)).ToListAsync()
            : new List<SysUser>();

        return logs.Select(l =>
        {
            var u = l.UserId.HasValue ? users.FirstOrDefault(x => x.Id == l.UserId.Value) : null;
            return new AuditLogDto
            {
                Id = l.Id,
                UserId = l.UserId,
                UserName = u?.Name ?? u?.Username ?? "",
                Action = l.Action,
                TargetType = l.TargetType,
                TargetId = l.TargetId,
                Detail = l.Detail,
                Ip = l.Ip,
                Ts = l.Ts
            };
        }).ToList();
    }
}
