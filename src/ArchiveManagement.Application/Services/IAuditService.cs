using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface IAuditService
{
    Task<List<AuditLogDto>> ListAsync(DateTime? from, DateTime? to, int limit = 200);
}

public class AuditService : IAuditService
{
    private readonly IFreeSql _db;
    public AuditService(IFreeSql db) => _db = db;

    public async Task<List<AuditLogDto>> ListAsync(DateTime? from, DateTime? to, int limit = 200)
    {
        var q = _db.Select<AuditLog>();
        if (from.HasValue) q = q.Where(a => a.Ts >= from.Value);
        if (to.HasValue) q = q.Where(a => a.Ts <= to.Value);
        var logs = await q.OrderByDescending(a => a.Id).Limit(limit).ToListAsync();
        var dtos = new List<AuditLogDto>();
        foreach (var l in logs)
        {
            var u = l.UserId.HasValue ? await _db.Select<SysUser>().Where(x => x.Id == l.UserId.Value).FirstAsync() : null;
            dtos.Add(new AuditLogDto
            {
                Id = l.Id, UserId = l.UserId,
                UserName = u?.Name ?? u?.Username ?? "",
                Action = l.Action, TargetType = l.TargetType, TargetId = l.TargetId,
                Detail = l.Detail, Ip = l.Ip, Ts = l.Ts
            });
        }
        return dtos;
    }
}
