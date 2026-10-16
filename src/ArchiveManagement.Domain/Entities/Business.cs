using ArchiveManagement.Domain.Enums;
using FreeSql.DataAnnotations;

namespace ArchiveManagement.Domain.Entities;

/// <summary>借阅申请 / 利用</summary>
public class BorrowRequest
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public long ApplicantId { get; set; }
    public string TargetType { get; set; } = string.Empty; // archive / file
    public long TargetId { get; set; }
    public int? Days { get; set; }                          // 申请借阅天数
    [Column(StringLength = -1)]
    public string? Reason { get; set; }
    public BorrowStatus Status { get; set; } = BorrowStatus.Pending;
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ExpireAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>鉴定销毁</summary>
public class Appraisal
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public string TargetType { get; set; } = string.Empty;
    public long TargetId { get; set; }
    [Column(StringLength = -1)]
    public string? ProposeReason { get; set; }
    public AppraisalStatus Status { get; set; } = AppraisalStatus.Pending;
    public long? ApprovedBy { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>审计日志（不可篡改、只读）</summary>
public class AuditLog
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public long? UserId { get; set; }
    public string? Action { get; set; }
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    [Column(StringLength = -1)]
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public DateTime Ts { get; set; } = DateTime.UtcNow;
}
