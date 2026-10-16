using ArchiveManagement.Domain.Enums;

namespace ArchiveManagement.Application.Dtos;

public class CreateBorrowDto
{
    public string TargetType { get; set; } = "archive"; // archive | file
    public long TargetId { get; set; }
    public int? Days { get; set; }                 // 申请借阅天数
    public string? Reason { get; set; }
}

public class BorrowDto
{
    public long Id { get; set; }
    public long ApplicantId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public long TargetId { get; set; }
    public string TargetTitle { get; set; } = string.Empty;
    public int? Days { get; set; }
    public string? Reason { get; set; }
    public BorrowStatus Status { get; set; }
    public long? ApprovedById { get; set; }
    public string ApprovedByName { get; set; } = string.Empty;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ExpireAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BorrowAuditDto
{
    public string? Comment { get; set; }
    public int? Days { get; set; } // 审批时确认的借阅天数(覆盖申请天数)
}

public class BorrowRenewDto
{
    public int? Days { get; set; }
}
