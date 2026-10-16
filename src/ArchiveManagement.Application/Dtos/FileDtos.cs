namespace ArchiveManagement.Application.Dtos;

public class FileItemDto
{
    public long Id { get; set; }
    public long ArchiveId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? DocDate { get; set; }
    public string? Author { get; set; }
    public int? Pages { get; set; }
    public int? SeqNo { get; set; }
    public string? FileNo { get; set; }
    public string? ProjectNo { get; set; }
    public string? DynamicFields { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFileItemDto
{
    public long ArchiveId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? DocDate { get; set; }
    public string? Author { get; set; }
    public int? Pages { get; set; }
    public int? SeqNo { get; set; }
    public string? FileNo { get; set; }
    public string? ProjectNo { get; set; }
    public string? DynamicFields { get; set; } // JSON
}
