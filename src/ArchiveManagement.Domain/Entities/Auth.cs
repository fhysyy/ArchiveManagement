using FreeSql.DataAnnotations;

namespace ArchiveManagement.Domain.Entities;

public class SysUser
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Dept { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? Salt { get; set; }
    public int Status { get; set; } = 1;
}

public class SysRole
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SysUserRole
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public long UserId { get; set; }
    public long RoleId { get; set; }
}

/// <summary>五维权限：角色 × 门类 × 操作 × 密级</summary>
public class SysPermission
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public long RoleId { get; set; }
    public long? CategoryId { get; set; }     // NULL=全部门类
    public string Op { get; set; } = string.Empty;   // view/edit/audit/destroy
    public string? MaxSecrecy { get; set; } = "公开"; // 该角色可见最高密级
}
