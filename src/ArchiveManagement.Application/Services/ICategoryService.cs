using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;

namespace ArchiveManagement.Application.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> ListAsync();
    Task<CategoryDto?> GetAsync(long id);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto);
    Task<CategoryDto?> UpdateAsync(long id, CreateCategoryDto dto);
    Task<bool> DeleteAsync(long id);
}

public class CategoryService : ICategoryService
{
    private readonly IFreeSql _db;
    public CategoryService(IFreeSql db) => _db = db;

    public async Task<List<CategoryDto>> ListAsync()
    {
        // 只返回启用门类
        var list = await _db.Select<ArchiveCategory>()
            .Where(c => c.Status == 1)
            .OrderBy(c => c.SortOrder).ToListAsync();
        return list.Select(ToDto).ToList()!;
    }

    public async Task<CategoryDto?> GetAsync(long id)
        => ToDto(await _db.Select<ArchiveCategory>().Where(c => c.Id == id).FirstAsync());

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        var e = new ArchiveCategory
        {
            Code = dto.Code, Name = dto.Name, Description = dto.Description,
            TemplateJson = dto.TemplateJson, FileTemplateJson = dto.FileTemplateJson,
            FkRule = dto.FkRule, DefaultRetention = dto.DefaultRetention
        };
        e.Id = await _db.Insert(e).ExecuteIdentityAsync();
        return ToDto(e)!;
    }

    public async Task<CategoryDto?> UpdateAsync(long id, CreateCategoryDto dto)
    {
        var e = await _db.Select<ArchiveCategory>().Where(c => c.Id == id).FirstAsync();
        if (e == null) return null;
        e.Code = dto.Code; e.Name = dto.Name; e.Description = dto.Description;
        e.TemplateJson = dto.TemplateJson; e.FileTemplateJson = dto.FileTemplateJson;
        e.FkRule = dto.FkRule; e.DefaultRetention = dto.DefaultRetention;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.Update<ArchiveCategory>().SetSource(e).ExecuteAffrowsAsync();
        return ToDto(e);
    }

    /// <summary>停用门类（逻辑删除）：契约要求"停用/删除"，避免物理删除导致档案门类悬空</summary>
    public async Task<bool> DeleteAsync(long id)
    {
        var used = await _db.Select<Archive>().Where(a => a.CategoryId == id && !a.IsDeleted).AnyAsync();
        if (used) throw new InvalidOperationException("该门类下仍有档案，不能停用");
        var aff = await _db.Update<ArchiveCategory>()
            .Where(c => c.Id == id)
            .Set(c => c.Status, 0)
            .ExecuteAffrowsAsync();
        return aff > 0;
    }

    private static CategoryDto? ToDto(ArchiveCategory? e) => e == null ? null : new CategoryDto
    {
        Id = e.Id, Code = e.Code, Name = e.Name, Description = e.Description,
        Status = e.Status, TemplateJson = e.TemplateJson, FileTemplateJson = e.FileTemplateJson,
        FkRule = e.FkRule, DefaultRetention = e.DefaultRetention
    };
}
