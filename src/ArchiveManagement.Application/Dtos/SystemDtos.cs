namespace ArchiveManagement.Application.Dtos;

// ===== 系统管理 T17 =====
public class HealthDto
{
    public string Status { get; set; } = "ok";
    public bool DbOk { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
}

public class BackupResultDto
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? File { get; set; }
    public DateTime Ts { get; set; }
}

public class UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Dept { get; set; }
    public int Status { get; set; }
    public string? RoleCode { get; set; }
    public string? RoleName { get; set; }
}

public class CreateUserDto
{
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Dept { get; set; }
    public string Password { get; set; } = string.Empty;
    public string RoleCode { get; set; } = "USER";
}

public class RoleDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class CreateRoleDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}

public class SetUserStatusDto
{
    public int Status { get; set; } // 1=启用 0=禁用
}

/// <summary>五维权限：角色 × 门类 × 操作 × 密级</summary>
public class PermissionDto
{
    public long Id { get; set; }
    public long RoleId { get; set; }
    public long? CategoryId { get; set; }
    public string CategoryName { get; set; } = "全部门类";
    public string Op { get; set; } = string.Empty;   // view/edit/audit/destroy
    public string? MaxSecrecy { get; set; }
}

public class SetPermissionsRequest
{
    public List<PermissionDto> Items { get; set; } = new();
}
