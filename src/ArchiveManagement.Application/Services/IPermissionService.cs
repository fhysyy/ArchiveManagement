using ArchiveManagement.Domain.Entities;
using FreeSql;

namespace ArchiveManagement.Application.Services;

/// <summary>当前用户的五维权限快照：角色 × 门类 × 操作 × 密级</summary>
public class UserPermission
{
    public long? UserId { get; set; }
    public long? RoleId { get; set; }
    public string RoleCode { get; set; } = "USER";
    public bool IsAdmin { get; set; }

    /// <summary>该角色可见最高密级（0=公开 … 4=绝密）</summary>
    public int MaxSecrecyLevel { get; set; }

    /// <summary>可访问门类；null 表示不限门类</summary>
    public HashSet<long>? AllowedCategoryIds { get; set; }

    /// <summary>门类 → 可执行操作集合（key=0 表示不限门类）</summary>
    public Dictionary<long, HashSet<string>> OpsByCategory { get; set; } = new();

    /// <summary>该角色是否配置了任何权限行。未配置时按"不限制"处理（向后兼容种子角色）</summary>
    public bool HasAnyPermission { get; set; }
}

public interface IPermissionService
{
    Task<UserPermission> ResolveAsync(string? username);
    bool Can(UserPermission p, long categoryId, string op);
    bool CanSee(UserPermission p, long categoryId, string? secrecyLevel);
}

public class PermissionService : IPermissionService
{
    /// <summary>密级顺序：公开 < 内部 < 秘密 < 机密 < 绝密</summary>
    private static readonly string[] SecrecyOrder = { "公开", "内部", "秘密", "机密", "绝密" };

    private readonly IFreeSql _db;
    public PermissionService(IFreeSql db) => _db = db;

    public static int LevelOf(string? secrecy)
    {
        if (string.IsNullOrWhiteSpace(secrecy)) return 0;
        var i = Array.IndexOf(SecrecyOrder, secrecy.Trim());
        return i < 0 ? 0 : i;
    }

    public async Task<UserPermission> ResolveAsync(string? username)
    {
        var p = new UserPermission();
        if (string.IsNullOrWhiteSpace(username)) return p;

        var user = await _db.Select<SysUser>().Where(u => u.Username == username).FirstAsync();
        if (user == null) return p;
        p.UserId = user.Id;

        var ur = await _db.Select<SysUserRole>().Where(x => x.UserId == user.Id).FirstAsync();
        SysRole? role = null;
        if (ur != null)
        {
            role = await _db.Select<SysRole>().Where(r => r.Id == ur.RoleId).FirstAsync();
            p.RoleId = role?.Id;
            p.RoleCode = role?.Code ?? "USER";
        }
        p.IsAdmin = string.Equals(p.RoleCode, "SYS_ADMIN", StringComparison.OrdinalIgnoreCase);
        if (p.IsAdmin) return p; // 系统管理员不做限制

        if (p.RoleId == null) return p;

        var perms = await _db.Select<SysPermission>().Where(x => x.RoleId == p.RoleId.Value).ToListAsync();
        p.HasAnyPermission = perms.Count > 0;

        foreach (var x in perms)
        {
            var key = x.CategoryId ?? 0;
            if (!p.OpsByCategory.TryGetValue(key, out var set))
                p.OpsByCategory[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(x.Op);
            p.MaxSecrecyLevel = Math.Max(p.MaxSecrecyLevel, LevelOf(x.MaxSecrecy));
        }

        // 仅当存在"限定门类"的行时才收敛可见门类（含 NULL 行表示不限门类）
        var specific = perms.Where(x => x.CategoryId.HasValue).Select(x => x.CategoryId!.Value).ToList();
        var hasWildcard = perms.Any(x => !x.CategoryId.HasValue);
        if (specific.Count > 0 && !hasWildcard)
            p.AllowedCategoryIds = new HashSet<long>(specific);

        return p;
    }

    public bool Can(UserPermission p, long categoryId, string op)
    {
        if (p.IsAdmin) return true;
        if (!p.HasAnyPermission) return true; // 未配置权限的角色：维持原有不限制行为

        if (p.AllowedCategoryIds != null && !p.AllowedCategoryIds.Contains(categoryId)) return false;

        if (p.OpsByCategory.TryGetValue(0, out var any) && any.Contains(op)) return true;
        if (p.OpsByCategory.TryGetValue(categoryId, out var set) && set.Contains(op)) return true;
        return false;
    }

    public bool CanSee(UserPermission p, long categoryId, string? secrecyLevel)
    {
        if (!Can(p, categoryId, "view")) return false;
        if (p.IsAdmin) return true;
        if (!p.HasAnyPermission) return true;
        // 无密级行时 MaxSecrecyLevel=0，仅能看到"公开"；为避免误伤，未显式配置密级时不过滤
        if (p.MaxSecrecyLevel <= 0) return true;
        return LevelOf(secrecyLevel) <= p.MaxSecrecyLevel;
    }
}
