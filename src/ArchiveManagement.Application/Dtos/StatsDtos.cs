namespace ArchiveManagement.Application.Dtos;

// ===== 统计报表 T15 =====
public class KvDto
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class CategoryStatDto
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class StatsOverviewDto
{
    public int Total { get; set; }
    public int Archived { get; set; }
    public int Draft { get; set; }
    public List<CategoryStatDto> ByCategory { get; set; } = new();
    public List<KvDto> ByYear { get; set; } = new();
    public List<KvDto> ByRetention { get; set; } = new();
    public List<KvDto> BySecrecy { get; set; } = new();
}

public class BorrowTargetStat
{
    public string TargetType { get; set; } = string.Empty;
    public long TargetId { get; set; }
    public string TargetTitle { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class BorrowApplicantStat
{
    public long ApplicantId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class BorrowStatsDto
{
    public int TotalBorrows { get; set; }
    public int PendingBorrows { get; set; }
    public List<BorrowTargetStat> ByTarget { get; set; } = new();
    public List<BorrowApplicantStat> ByApplicant { get; set; } = new();
}

public class DueWarningItem
{
    public ArchiveDto Archive { get; set; } = new();
    public int ExpiryYear { get; set; }
    public bool Appraised { get; set; }
}

public class DueWarningDto
{
    public int Total { get; set; }
    public List<DueWarningItem> Items { get; set; } = new();
}
