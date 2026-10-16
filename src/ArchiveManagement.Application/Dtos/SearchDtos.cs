namespace ArchiveManagement.Application.Dtos;

/// <summary>条件检索请求：支持门类/年度区间/题名/责任者/保管期限/密级/状态 多条件组合</summary>
public class SearchArchiveDto
{
    public long? CategoryId { get; set; }
    public int? YearStart { get; set; }
    public int? YearEnd { get; set; }
    public string? Title { get; set; }          // 题名(模糊)
    public string? Author { get; set; }         // 责任者(模糊，命中 dynamic_fields 或归档单位)
    public string? Retention { get; set; }      // 保管期限(精确)
    public string? SecrecyLevel { get; set; }   // 密级(精确)
    public string? Status { get; set; }         // 档案状态(枚举名)
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "Id";  // Id | Year | Title | CreatedAt
    public string? SortDir { get; set; } = "desc"; // asc | desc
}

/// <summary>分页检索结果</summary>
public class PagedArchiveResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<ArchiveDto> Items { get; set; } = new();
}
