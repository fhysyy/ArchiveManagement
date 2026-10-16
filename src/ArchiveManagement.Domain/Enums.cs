namespace ArchiveManagement.Domain.Enums;

/// <summary>档案状态机：草稿 → 已归档（退回/已销毁）</summary>
public enum ArchiveStatus : int
{
    Draft = 0,      // 草稿
    Submitted = 4,  // 待审核（已提交归档）
    Archived = 1,   // 已归档
    Rejected = 3,   // 退回
    Destroyed = 2   // 已销毁
}

public enum BorrowStatus : int
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Returned = 3,
    Expired = 4
}

public enum AppraisalStatus : int
{
    Pending = 0,
    Approved = 1,
    Destroyed = 2,
    Kept = 3
}
