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
    /// <summary>已存在的鉴定任务 Id（Pending/Approved 视为在途），无则为 null</summary>
    public long? AppraisalId { get; set; }
    public string? AppraisalStatus { get; set; }
}

/// <summary>鉴定任务分页结果</summary>
public class PagedAppraisalResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<AppraisalDto> Items { get; set; } = new();
}

/// <summary>到期清单分页结果</summary>
public class PagedDueResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<DueArchiveDto> Items { get; set; } = new();
}
