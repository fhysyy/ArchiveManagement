using ArchiveManagement.Domain.Enums;
using FreeSql.DataAnnotations;

namespace ArchiveManagement.Domain.Entities;

/// <summary>全宗（单全宗时仅一条，fonds_id 预留多全宗扩展）</summary>
public class Fonds
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>档案门类：可配置元数据模板，新增门类不改表结构</summary>
public class ArchiveCategory
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public long FondsId { get; set; } = 1;
    public string Code { get; set; } = string.Empty;        // DZ/KY/JJ...
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
    public int SortOrder { get; set; } = 0;
    [Column(StringLength = -1)]                              // nvarchar(max)
    public string TemplateJson { get; set; } = "[]";         // 案卷级元数据模板 JSON
    [Column(StringLength = -1)]                              // nvarchar(max)
    public string FileTemplateJson { get; set; } = "[]";     // 卷内文件级元数据模板 JSON
    public string? FkRule { get; set; }                      // {fonds}-{code}-{year}-{seq}
    public string? DefaultRetention { get; set; } = "30年";
    public string? AllowedAttachTypes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>档案（案卷级）。基建的“项目”用 ParentId 自引用表达</summary>
public class Archive
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public long FondsId { get; set; } = 1;
    public long CategoryId { get; set; }
    public long? ParentId { get; set; }                      // 基建：项目(父)→案卷(子)
    public bool IsProject { get; set; } = false;             // 1=基建项目节点
    public string ArchiveNo { get; set; } = string.Empty;    // 档号
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? Retention { get; set; }                  // 永久/30年/10年
    public string? SecrecyLevel { get; set; } = "公开";      // 公开/内部/秘密/机密/绝密
    public string? OrgUnit { get; set; }
    public string? Description { get; set; }
    public ArchiveStatus Status { get; set; } = ArchiveStatus.Draft;
    [Column(StringLength = -1)]
    public string? DynamicFields { get; set; }               // 门类自定义元数据(JSON)
    public long? CreatorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>文件（件级）</summary>
public class FileItem
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public long ArchiveId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? DocDate { get; set; }
    public string? Author { get; set; }
    public int? Pages { get; set; }
    public int? SeqNo { get; set; }
    public string? FileNo { get; set; }
    public string? ProjectNo { get; set; }
    [Column(StringLength = -1)]
    public string? DynamicFields { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>附件（电子文件级）：实体存对象存储，DB 仅存 key + 哈希</summary>
public class Attachment
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public long FileItemId { get; set; }
    public string FileName { get; set; } = string.Empty;     // 存储名(对象 key)
    public string? OriginalName { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string? Hash { get; set; }                        // SHA256 防篡改
    public int Version { get; set; } = 1;
    [Column(StringLength = -1)]
    public string? OcrText { get; set; }
    public bool WatermarkFlag { get; set; } = true;
    public long? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
