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
