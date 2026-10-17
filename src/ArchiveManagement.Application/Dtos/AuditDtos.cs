namespace ArchiveManagement.Application.Dtos;

public class AuditLogDto
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Action { get; set; }
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public DateTime Ts { get; set; }
}

public class PagedAuditResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<AuditLogDto> Items { get; set; } = new();
}
