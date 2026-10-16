using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Infrastructure;

namespace ArchiveManagement.Application.Services;

public interface IAuthService
{
    Task<(bool ok, LoginResultDto? result)> LoginAsync(LoginDto dto);
}

public class AuthService : IAuthService
{
    private readonly IFreeSql _db;
    public AuthService(IFreeSql db) => _db = db;

    public async Task<(bool ok, LoginResultDto? result)> LoginAsync(LoginDto dto)
    {
        var user = await _db.Select<SysUser>().Where(u => u.Username == dto.Username).FirstAsync();
        if (user == null || !SecurityHelper.Verify(dto.Password, user.PasswordHash, user.Salt ?? string.Empty))
            return (false, null);

        var ur = await _db.Select<SysUserRole>().Where(x => x.UserId == user.Id).FirstAsync();
        var role = ur == null ? null : await _db.Select<SysRole>().Where(r => r.Id == ur.RoleId).FirstAsync();

        var result = new LoginResultDto
        {
            Username = user.Username,
            Name = user.Name,
            Role = role?.Code ?? "USER"
        };
        return (true, result);
    }
}
