namespace ArchiveManagement.Application.Dtos;

public class AttachmentDto
{
    public long Id { get; set; }
    public long FileItemId { get; set; }
    public string FileName { get; set; } = string.Empty;   // 对象存储 key
    public string? OriginalName { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string? Hash { get; set; }                       // SHA256 防篡改
    public int Version { get; set; }
    public bool WatermarkFlag { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class AttachmentUploadResultDto
{
    public long Id { get; set; }
    public string? OriginalName { get; set; }
    public string? Hash { get; set; }
    public long? SizeBytes { get; set; }
}
