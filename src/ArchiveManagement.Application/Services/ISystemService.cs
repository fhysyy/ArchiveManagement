using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Infrastructure;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface ISystemService
{
    Task<HealthDto> HealthAsync();
    Task<BackupResultDto> BackupAsync(string? operatorName);
    Task<List<UserDto>> ListUsersAsync();
    Task<UserDto> CreateUserAsync(CreateUserDto dto);
    Task<bool> DeleteUserAsync(long id);
    Task<bool> ChangePasswordAsync(long id, string newPassword);
    Task<bool> SetUserStatusAsync(long id, int status);
    Task<List<RoleDto>> ListRolesAsync();
    Task<RoleDto> CreateRoleAsync(CreateRoleDto dto);
    Task<bool> DeleteRoleAsync(long id);

    Task<List<PermissionDto>> ListPermissionsAsync(long roleId);
    Task<List<PermissionDto>> SetPermissionsAsync(long roleId, List<PermissionDto> items);
}

public class SystemService : ISystemService
{
    private readonly IFreeSql _db;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _cfg;
    public SystemService(IFreeSql db, Microsoft.Extensions.Configuration.IConfiguration cfg)
        => (_db, _cfg) = (db, cfg);

    public async Task<HealthDto> HealthAsync()
    {
        bool dbOk = false;
        try
        {
            await _db.Select<SysUser>().CountAsync();
            dbOk = true;
        }
        catch { /* 连不上库仅标记，不抛 */ }
        return new HealthDto
        {
            Status = dbOk ? "ok" : "degraded",
            DbOk = dbOk,
            Time = DateTime.UtcNow.ToString("o")
        };
    }

    /// <summary>
    /// 真实备份：对当前库执行 SQL Server 的 BACKUP DATABASE，落到 Backup:Path 目录。
    /// 备份文件按「库名_yyyyMMdd_HHmmss.bak」命名；失败时返回原因并落审计。
    /// </summary>
    public async Task<BackupResultDto> BackupAsync(string? operatorName)
    {
        string? file = null;
        try
        {
            var dbName = ResolveDatabaseName();
            if (string.IsNullOrWhiteSpace(dbName))
                return new BackupResultDto { Ok = false, Message = "无法从连接串解析数据库名", Ts = DateTime.UtcNow };

            var dir = _cfg["Backup:Path"];
            if (string.IsNullOrWhiteSpace(dir))
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                   "ArchiveManagement", "backups");
            Directory.CreateDirectory(dir);

            file = Path.Combine(dir, $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
            // WITH INIT 覆盖同名文件；COMPRESSION 在 Express 版不支持，故不使用
            var sql = $"BACKUP DATABASE [{dbName.Replace("]", "]]")}] TO DISK = N'{file.Replace("'", "''")}' WITH INIT, NAME = N'ArchiveManagement 全量备份', DESCRIPTION = N'{DateTime.Now:yyyy-MM-dd HH:mm:ss}'";
            await _db.Ado.ExecuteNonQueryAsync(sql);

            long? uid = null;
            if (!string.IsNullOrWhiteSpace(operatorName))
            {
                var u = await _db.Select<SysUser>().Where(x => x.Username == operatorName).FirstAsync();
                uid = u?.Id;
            }
            await _db.Insert(new AuditLog
            {
                UserId = uid, Action = "system.backup", TargetType = "System",
                TargetId = null, Detail = $"数据库备份成功：{file}", Ts = DateTime.UtcNow
            }).ExecuteAffrowsAsync();

            var sizeMb = File.Exists(file) ? new FileInfo(file).Length / 1024.0 / 1024.0 : 0;
            return new BackupResultDto
            {
                Ok = true,
                Message = $"备份成功（{sizeMb:F1} MB）",
                File = file,
                Ts = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            try
            {
                await _db.Insert(new AuditLog
                {
                    Action = "system.backup", TargetType = "System",
                    Detail = "数据库备份失败：" + ex.Message, Ts = DateTime.UtcNow
                }).ExecuteAffrowsAsync();
            }
            catch { /* 审计写不进也不能影响返回 */ }
            // 失败时清理可能生成的空文件
            if (file != null && File.Exists(file) && new FileInfo(file).Length == 0)
                try { File.Delete(file); } catch { }
            return new BackupResultDto { Ok = false, Message = ex.Message, Ts = DateTime.UtcNow };
        }
    }

    /// <summary>从连接串解析数据库名（Database= / Initial Catalog=）</summary>
    private string ResolveDatabaseName()
    {
        var cs = _cfg["ConnectionStrings:SqlServer"] ?? string.Empty;
        foreach (var part in cs.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var k = kv[0].Trim();
            if (string.Equals(k, "Database", StringComparison.OrdinalIgnoreCase)
                || string.Equals(k, "Initial Catalog", StringComparison.OrdinalIgnoreCase))
                return kv[1].Trim();
        }
        return string.Empty;
    }

    public async Task<List<UserDto>> ListUsersAsync()
    {
        var users = await _db.Select<SysUser>().ToListAsync();
        var ur = await _db.Select<SysUserRole>().ToListAsync();
        var roles = await _db.Select<SysRole>().ToListAsync();
        var roleOf = ur.ToDictionary(x => x.UserId, x => x.RoleId);
        var result = new List<UserDto>();
        foreach (var u in users)
        {
            long? rid = roleOf.TryGetValue(u.Id, out var r) ? r : null;
            var role = rid.HasValue ? roles.FirstOrDefault(x => x.Id == rid.Value) : null;
            result.Add(new UserDto
            {
                Id = u.Id, Username = u.Username, Name = u.Name, Dept = u.Dept, Status = u.Status,
                RoleCode = role?.Code, RoleName = role?.Name
            });
        }
        return result;
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
        if (await _db.Select<SysUser>().Where(u => u.Username == dto.Username).AnyAsync())
            throw new InvalidOperationException("用户名已存在");
        SecurityHelper.HashPassword(dto.Password, out var h, out var s);
        var e = new SysUser
        {
            Username = dto.Username, Name = dto.Name, Dept = dto.Dept,
            PasswordHash = h, Salt = s, Status = 1
        };
        e.Id = await _db.Insert(e).ExecuteIdentityAsync();
        var role = await _db.Select<SysRole>().Where(r => r.Code == dto.RoleCode).FirstAsync();
        if (role != null)
            await _db.Insert(new SysUserRole { UserId = e.Id, RoleId = role.Id }).ExecuteAffrowsAsync();
        return new UserDto
        {
            Id = e.Id, Username = e.Username, Name = e.Name, Dept = e.Dept, Status = e.Status,
            RoleCode = role?.Code, RoleName = role?.Name
        };
    }

    public async Task<bool> DeleteUserAsync(long id)
    {
        await _db.Delete<SysUserRole>().Where(x => x.UserId == id).ExecuteAffrowsAsync();
        return await _db.Delete<SysUser>().Where(u => u.Id == id).ExecuteAffrowsAsync() > 0;
    }

    public async Task<bool> ChangePasswordAsync(long id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword)) throw new ArgumentException("新密码不能为空");
        var u = await _db.Select<SysUser>().Where(x => x.Id == id).FirstAsync();
        if (u == null) return false;
        SecurityHelper.HashPassword(newPassword, out var h, out var s);
        return await _db.Update<SysUser>()
            .Where(x => x.Id == id)
            .Set(x => x.PasswordHash, h).Set(x => x.Salt, s)
            .ExecuteAffrowsAsync() > 0;
    }

    /// <summary>启用(1) / 禁用(0)</summary>
    public async Task<bool> SetUserStatusAsync(long id, int status)
    {
        return await _db.Update<SysUser>()
            .Where(x => x.Id == id)
            .Set(x => x.Status, status)
            .ExecuteAffrowsAsync() > 0;
    }

    public async Task<List<RoleDto>> ListRolesAsync()
    {
        var roles = await _db.Select<SysRole>().ToListAsync();
        return roles.Select(r => new RoleDto { Id = r.Id, Code = r.Code, Name = r.Name }).ToList();
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleDto dto)
    {
        if (await _db.Select<SysRole>().Where(r => r.Code == dto.Code).AnyAsync())
            throw new InvalidOperationException("角色编码已存在");
        var e = new SysRole { Code = dto.Code, Name = dto.Name };
        e.Id = await _db.Insert(e).ExecuteIdentityAsync();
        return new RoleDto { Id = e.Id, Code = e.Code, Name = e.Name };
    }

    public async Task<bool> DeleteRoleAsync(long id)
    {
        await _db.Delete<SysUserRole>().Where(x => x.RoleId == id).ExecuteAffrowsAsync();
        await _db.Delete<SysPermission>().Where(x => x.RoleId == id).ExecuteAffrowsAsync();
        return await _db.Delete<SysRole>().Where(r => r.Id == id).ExecuteAffrowsAsync() > 0;
    }

    public async Task<List<PermissionDto>> ListPermissionsAsync(long roleId)
    {
        var list = await _db.Select<SysPermission>().Where(p => p.RoleId == roleId).ToListAsync();
        var cats = await _db.Select<ArchiveCategory>().ToListAsync();
        return list.Select(p => new PermissionDto
        {
            Id = p.Id, RoleId = p.RoleId, CategoryId = p.CategoryId,
            CategoryName = p.CategoryId.HasValue
                ? (cats.FirstOrDefault(c => c.Id == p.CategoryId.Value)?.Name ?? $"门类#{p.CategoryId}")
                : "全部门类",
            Op = p.Op, MaxSecrecy = p.MaxSecrecy
        }).ToList();
    }

    /// <summary>整量覆盖式配置某角色的五维权限</summary>
    public async Task<List<PermissionDto>> SetPermissionsAsync(long roleId, List<PermissionDto> items)
    {
        await _db.Delete<SysPermission>().Where(p => p.RoleId == roleId).ExecuteAffrowsAsync();
        foreach (var it in items)
        {
            await _db.Insert(new SysPermission
            {
                RoleId = roleId, CategoryId = it.CategoryId,
                Op = it.Op, MaxSecrecy = it.MaxSecrecy ?? "公开"
            }).ExecuteAffrowsAsync();
        }
        return await ListPermissionsAsync(roleId);
    }
}
