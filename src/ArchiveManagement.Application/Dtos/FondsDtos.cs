namespace ArchiveManagement.Application.Dtos;

public class FondsDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;      // 全宗号
    public string Name { get; set; } = string.Empty;      // 全宗名称
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // 概览（只读统计）
    public int CategoryCount { get; set; }
    public int ArchiveCount { get; set; }
}

public class SaveFondsDto
{
    public long Id { get; set; }          // 0 表示新增
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
}
