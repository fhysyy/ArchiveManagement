using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;

namespace ArchiveManagement.Application.Services;

public interface IFondsService
{
    Task<List<FondsDto>> ListAsync();
    Task<FondsDto?> GetAsync(long id);
    Task<FondsDto> SaveAsync(SaveFondsDto dto, string? operatorName);
}

/// <summary>
/// 全宗管理。一期为单全宗（档号规则里的 {fonds} 取自第一条全宗），
/// 表结构与接口按多全宗预留。
/// </summary>
public class FondsService : IFondsService
{
    private readonly IFreeSql _db;
    private readonly IAuditLogger _audit;

    public FondsService(IFreeSql db, IAuditLogger audit) => (_db, _audit) = (db, audit);

    public async Task<List<FondsDto>> ListAsync()
    {
        var list = await _db.Select<Fonds>().OrderBy(f => f.Id).ToListAsync();
        var result = new List<FondsDto>();
        foreach (var f in list) result.Add(await ToDto(f));
        return result;
    }

    public async Task<FondsDto?> GetAsync(long id)
    {
        var e = id > 0
            ? await _db.Select<Fonds>().Where(f => f.Id == id).FirstAsync()
            : await _db.Select<Fonds>().OrderBy(f => f.Id).FirstAsync(); // 单全宗：默认第一条
        return e == null ? null : await ToDto(e);
    }

    public async Task<FondsDto> SaveAsync(SaveFondsDto dto, string? operatorName)
    {
        if (string.IsNullOrWhiteSpace(dto.Code)) throw new ArgumentException("全宗号不能为空");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("全宗名称不能为空");

        // 全宗号唯一（排除自身）
        var dup = await _db.Select<Fonds>()
            .Where(f => f.Code == dto.Code && f.Id != dto.Id).AnyAsync();
        if (dup) throw new InvalidOperationException($"全宗号『{dto.Code}』已存在");

        Fonds e;
        bool isNew = dto.Id <= 0;
        if (isNew)
        {
            e = new Fonds { CreatedAt = DateTime.UtcNow };
        }
        else
        {
            e = await _db.Select<Fonds>().Where(f => f.Id == dto.Id).FirstAsync()
                ?? throw new ArgumentException("全宗不存在");
        }

        e.Code = dto.Code.Trim();
        e.Name = dto.Name.Trim();
        e.Description = dto.Description;
        e.Status = dto.Status;
        e.UpdatedAt = DateTime.UtcNow;

        if (isNew) e.Id = await _db.Insert(e).ExecuteIdentityAsync();
        else await _db.Update<Fonds>().SetSource(e).ExecuteAffrowsAsync();

        await _audit.LogAsync(isNew ? "fonds.create" : "fonds.update", "Fonds", e.Id,
            $"{(isNew ? "新增" : "修改")}全宗 {e.Code} {e.Name}");

        return await ToDto(e);
    }

    private async Task<FondsDto> ToDto(Fonds f) => new()
    {
        Id = f.Id,
        Code = f.Code,
        Name = f.Name,
        Description = f.Description,
        Status = f.Status,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt,
        CategoryCount = (int)await _db.Select<ArchiveCategory>().Where(c => c.Status == 1).CountAsync(),
        ArchiveCount = (int)await _db.Select<Archive>().Where(a => !a.IsDeleted).CountAsync()
    };
}
