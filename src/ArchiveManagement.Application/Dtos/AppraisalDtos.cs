using ArchiveManagement.Domain.Enums;

namespace ArchiveManagement.Application.Dtos;

public class CreateAppraisalDto
{
    public string TargetType { get; set; } = "archive"; // archive | file
    public long TargetId { get; set; }
    public string? ProposeReason { get; set; }
}

public class AppraisalDto
{
    public long Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public long TargetId { get; set; }
    public string TargetTitle { get; set; } = string.Empty;
    public string? ProposeReason { get; set; }
    public AppraisalStatus Status { get; set; }
    public long? ApprovedById { get; set; }
    public string ApprovedByName { get; set; } = string.Empty;
    public DateTime? ExecutedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AppraisalAuditDto
{
    public string Action { get; set; } = string.Empty; // keep | destroy
    public string? Comment { get; set; }
}

/// <summary>到期保管期限清单项</summary>
public class DueArchiveDto
{
    public ArchiveDto Archive { get; set; } = new();
    public int ExpiryYear { get; set; }
}
