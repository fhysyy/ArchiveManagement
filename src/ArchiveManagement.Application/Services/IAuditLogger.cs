using ArchiveManagement.Domain.Entities;
using FreeSql;

namespace ArchiveManagement.Application.Services;

/// <summary>当前操作上下文抽象（由展现层实现，避免 Application 依赖 ASP.NET Core）</summary>
public interface ICurrentUser
{
    string? UserName { get; }
    string? IpAddress { get; }
}

public interface IAuditLogger
{
    /// <summary>记录审计：操作人与 IP 自动取自当前上下文</summary>
    Task LogAsync(string action, string targetType, long? targetId = null, string? detail = null);
}

public class AuditLogger : IAuditLogger
{
    private readonly IFreeSql _db;
    private readonly ICurrentUser _current;
    public AuditLogger(IFreeSql db, ICurrentUser current) => (_db, _current) = (db, current);

    public async Task LogAsync(string action, string targetType, long? targetId = null, string? detail = null)
    {
        var username = _current.UserName;
        long? userId = null;
        if (!string.IsNullOrWhiteSpace(username))
        {
            var u = await _db.Select<SysUser>().Where(x => x.Username == username).FirstAsync();
            userId = u?.Id;
        }

        await _db.Insert(new AuditLog
        {
            UserId = userId, Action = action, TargetType = targetType,
            TargetId = targetId, Detail = detail, Ip = _current.IpAddress, Ts = DateTime.UtcNow
        }).ExecuteAffrowsAsync();
    }
}
