namespace ArchiveManagement.Application.Dtos;

public class ArchiveDto
{
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public long? ParentId { get; set; }
    public bool IsProject { get; set; }
    public string ArchiveNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? Retention { get; set; }
    public string? SecrecyLevel { get; set; }
    public string? OrgUnit { get; set; }
    public string? DynamicFields { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreateArchiveDto
{
    public long CategoryId { get; set; }
    public long? ParentId { get; set; }
    public bool IsProject { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? Retention { get; set; }
    public string? SecrecyLevel { get; set; }
    public string? OrgUnit { get; set; }
    public string? DynamicFields { get; set; } // JSON
}

/// <summary>T13 审核请求</summary>
public class ArchiveAuditDto
{
    public string Action { get; set; } = string.Empty; // approve | reject
    public string? Comment { get; set; }
}

/// <summary>三级树：档案 → 文件 → 附件</summary>
public class FileTreeDto
{
    public FileItemDto File { get; set; } = new();
    public List<AttachmentDto> Attachments { get; set; } = new();
}

public class ArchiveTreeDto
{
    public ArchiveDto Archive { get; set; } = new();
    public List<FileTreeDto> Files { get; set; } = new();
}

/// <summary>批量导入（一行一条档案）</summary>
public class BatchImportRequest
{
    public long CategoryId { get; set; }
    public List<CreateArchiveDto> Items { get; set; } = new();
}

public class BatchImportResult
{
    public int Created { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<ArchiveDto> Items { get; set; } = new();
}
