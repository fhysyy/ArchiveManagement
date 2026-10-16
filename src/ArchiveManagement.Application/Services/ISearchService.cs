using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Domain.Enums;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface ISearchService
{
    Task<PagedArchiveResult> SearchAsync(SearchArchiveDto dto);
    Task<List<string>> SuggestAsync(string q, int limit = 10);
}

public class SearchService : ISearchService
{
    private readonly IFreeSql _db;
    public SearchService(IFreeSql db) => _db = db;

    public async Task<PagedArchiveResult> SearchAsync(SearchArchiveDto dto)
    {
        var q = _db.Select<Archive>();

        if (dto.CategoryId.HasValue)
            q = q.Where(a => a.CategoryId == dto.CategoryId.Value);
        if (dto.YearStart.HasValue)
            q = q.Where(a => a.Year >= dto.YearStart.Value);
        if (dto.YearEnd.HasValue)
            q = q.Where(a => a.Year <= dto.YearEnd.Value);
        if (!string.IsNullOrWhiteSpace(dto.Title))
            q = q.Where(a => a.Title.Contains(dto.Title));
        if (!string.IsNullOrWhiteSpace(dto.Author))
            q = q.Where(a => (a.OrgUnit != null && a.OrgUnit.Contains(dto.Author))
                          || (a.DynamicFields != null && a.DynamicFields.Contains(dto.Author)));
        if (!string.IsNullOrWhiteSpace(dto.Retention))
            q = q.Where(a => a.Retention == dto.Retention);
        if (!string.IsNullOrWhiteSpace(dto.SecrecyLevel))
            q = q.Where(a => a.SecrecyLevel == dto.SecrecyLevel);
        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<ArchiveStatus>(dto.Status, out var st))
            q = q.Where(a => a.Status == st);

        var total = await q.CountAsync();

        // 排序
        string order = (dto.SortDir ?? "desc").Trim().ToLower() == "asc" ? "ASC" : "DESC";
        string field = dto.SortBy switch
        {
            "Year" => "Year",
            "Title" => "Title",
            "CreatedAt" => "Id", // Archive 无 CreatedAt，退化为 Id
            _ => "Id"
        };
        q = q.OrderBy($"{field} {order}");

        int page = dto.Page < 1 ? 1 : dto.Page;
        int size = dto.PageSize < 1 ? 20 : (dto.PageSize > 200 ? 200 : dto.PageSize);
        var items = await q.Page(page, size).ToListAsync();

        return new PagedArchiveResult
        {
            Total = (int)total,
            Page = page,
            PageSize = size,
            TotalPages = (int)Math.Ceiling(total / (double)size),
            Items = items.Select(ToDto).ToList()!
        };
    }

    /// <summary>检索联想：按题名/档号前缀匹配</summary>
    public async Task<List<string>> SuggestAsync(string q, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(q)) return new List<string>();
        var list = await _db.Select<Archive>()
            .Where(a => a.Title.Contains(q) || a.ArchiveNo.Contains(q))
            .Limit(limit).ToListAsync();
        return list.Select(a => a.Title).Distinct().Take(limit).ToList();
    }

    private static ArchiveDto ToDto(Archive a) => new()
    {
        Id = a.Id, CategoryId = a.CategoryId, ParentId = a.ParentId, IsProject = a.IsProject,
        ArchiveNo = a.ArchiveNo, Title = a.Title, Year = a.Year, Retention = a.Retention,
        SecrecyLevel = a.SecrecyLevel, OrgUnit = a.OrgUnit, DynamicFields = a.DynamicFields,
        Status = a.Status.ToString()
    };
}
