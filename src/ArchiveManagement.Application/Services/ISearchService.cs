using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Domain.Enums;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface ISearchService
{
    Task<PagedArchiveResult> SearchAsync(SearchArchiveDto dto, string? username = null);
    Task<List<string>> SuggestAsync(string q, int limit = 10);
}

public class SearchService : ISearchService
{
    private readonly IFreeSql _db;
    private readonly IPermissionService _perm;
    public SearchService(IFreeSql db, IPermissionService perm) => (_db, _perm) = (db, perm);

    public async Task<PagedArchiveResult> SearchAsync(SearchArchiveDto dto, string? username = null)
    {
        var p = await _perm.ResolveAsync(username);
        var q = _db.Select<Archive>().Where(a => !a.IsDeleted);

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

        // 自定义字段条件（dynamic_fields 里的 JSON key）：合并成一条原生 WHERE，参数用字典
        var parms = new Dictionary<string, object>();
        var conds = new List<string>();
        int seq = 0;
        string P(object v)
        {
            var name = $"mp{seq++}";
            parms[name] = v;
            return "@" + name;
        }
        if (dto.Meta != null)
            foreach (var f in dto.Meta)
            {
                var c = BuildMetaCondition(f, P);
                if (c != null) conds.Add(c);
            }
        if (conds.Count > 0)
            q = q.Where(string.Join(" AND ", conds), parms);

        // 权限：可见门类收窄下推到 DB，减少取回量
        if (!p.IsAdmin && p.AllowedCategoryIds != null)
            q = q.Where(a => p.AllowedCategoryIds.Contains(a.CategoryId));

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

        // 密级过滤在内存完成（便于统一走 CanSee），再做分页
        var all = await q.ToListAsync();
        var visible = all.Where(a => _perm.CanSee(p, a.CategoryId, a.SecrecyLevel)).ToList();

        int page = dto.Page < 1 ? 1 : dto.Page;
        int size = dto.PageSize < 1 ? 20 : (dto.PageSize > 200 ? 200 : dto.PageSize);
        var items = visible.Skip((page - 1) * size).Take(size).ToList();
        var total = visible.Count;

        return new PagedArchiveResult
        {
            Total = total,
            Page = page,
            PageSize = size,
            TotalPages = (int)Math.Ceiling(total / (double)size),
            Items = items.Select(ToDto).ToList()!
        };
    }

    // ===== 自定义字段检索（SQL Server JSON_VALUE）=====
    // key 只允许字母/数字/下划线且非数字开头，杜绝 JSON path 注入
    private static readonly System.Text.RegularExpressions.Regex KeyRegex =
        new("^[A-Za-z_][A-Za-z0-9_]{0,63}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// 拼自定义字段条件：ISJSON 守卫 + JSON_VALUE 取值，值全部参数化。
    /// 说明：JSON_VALUE 返回 nvarchar(4000)，数字/日期比较用 TRY_CAST。
    /// </summary>
    private static string? BuildMetaCondition(MetaFilterDto f, Func<object, string> p)
    {
        if (string.IsNullOrWhiteSpace(f.Key) || !KeyRegex.IsMatch(f.Key)) return null;
        var val = f.Value?.Trim();
        if (string.IsNullOrWhiteSpace(val)) return null;

        // key 已过正则白名单，可安全拼入 JSON path 常量；查询值一律走命名参数
        string col = $"JSON_VALUE([DynamicFields], '$.{f.Key}')";
        string guard = "ISJSON([DynamicFields]) = 1";
        var op = (f.Op ?? "contains").Trim().ToLowerInvariant();

        switch (op)
        {
            case "eq":
                return $"{guard} AND {col} = {p(val)}";
            case "gt":
                return $"{guard} AND TRY_CAST({col} AS DECIMAL(18,4)) > {p(ToDecimal(val))}";
            case "lt":
                return $"{guard} AND TRY_CAST({col} AS DECIMAL(18,4)) < {p(ToDecimal(val))}";
            case "between":
                var parts = val.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length != 2) return null;
                return $"{guard} AND TRY_CAST({col} AS DECIMAL(18,4)) BETWEEN {p(ToDecimal(parts[0]))} AND {p(ToDecimal(parts[1]))}";
            default: // contains
                return $"{guard} AND {col} LIKE {p($"%{val}%")}";
        }
    }

    private static decimal ToDecimal(string s)
        => decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;

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
