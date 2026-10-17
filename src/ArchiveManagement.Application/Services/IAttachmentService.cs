using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Domain.Enums;
using ArchiveManagement.Infrastructure;
using FreeSql;
using System.Security.Cryptography;

namespace ArchiveManagement.Application.Services;

public interface IAttachmentService
{
    Task<List<AttachmentDto>> ListByFileAsync(long fileItemId);
    Task<AttachmentUploadResultDto> UploadAsync(long fileItemId, string fileName, string? contentType, long size, Stream content, long? uploaderId);
    /// <summary>下载/预览：校验查看权限或已批准的借阅，未授权抛异常</summary>
    Task<(Stream? stream, AttachmentDto? meta)> DownloadAsync(long id, string? username = null);
    Task<bool> DeleteAsync(long id);
}

public class AttachmentService : IAttachmentService
{
    private readonly IFreeSql _db;
    private readonly IObjectStorage _storage;
    private readonly IPermissionService _perm;
    private readonly IAuditLogger _audit;
    private readonly IWatermarkService _watermark;

    public AttachmentService(IFreeSql db, IObjectStorage storage, IPermissionService perm,
        IAuditLogger audit, IWatermarkService watermark)
        => (_db, _storage, _perm, _audit, _watermark) = (db, storage, perm, audit, watermark);

    public async Task<List<AttachmentDto>> ListByFileAsync(long fileItemId)
    {
        var list = await _db.Select<Attachment>()
            .Where(a => a.FileItemId == fileItemId)
            .OrderByDescending(a => a.Id).ToListAsync();
        return list.Select(ToDto).ToList()!;
    }

    public async Task<AttachmentUploadResultDto> UploadAsync(long fileItemId, string fileName, string? contentType, long size, Stream content, long? uploaderId)
    {
        // 先算 SHA256（边读边算），再落对象存储
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        ms.Position = 0;
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(ms);
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        ms.Position = 0;

        var key = await _storage.SaveAsync(ms, fileName);
        var e = new Attachment
        {
            FileItemId = fileItemId,
            FileName = key,                 // 对象 key（本地实现为物理路径）
            OriginalName = fileName,
            MimeType = contentType,
            SizeBytes = size,
            Hash = hash,
            UploadedBy = uploaderId,
            UploadedAt = DateTime.UtcNow
        };
        e.Id = await _db.Insert(e).ExecuteIdentityAsync();
        await _audit.LogAsync("attachment.upload", "Attachment", e.Id,
            $"上传附件 {e.OriginalName}（SHA256={e.Hash}）");
        return new AttachmentUploadResultDto
        {
            Id = e.Id,
            OriginalName = e.OriginalName,
            Hash = e.Hash,
            SizeBytes = e.SizeBytes
        };
    }

    public async Task<(Stream? stream, AttachmentDto? meta)> DownloadAsync(long id, string? username = null)
    {
        var e = await _db.Select<Attachment>().Where(a => a.Id == id).FirstAsync();
        if (e == null) return (null, null);

        // 回溯到所属档案，做权限校验
        var file = await _db.Select<FileItem>().Where(f => f.Id == e.FileItemId).FirstAsync();
        if (file != null)
        {
            var archive = await _db.Select<Archive>().Where(a => a.Id == file.ArchiveId).FirstAsync();
            if (archive != null)
            {
                var p = await _perm.ResolveAsync(username);
                var allowed = _perm.CanSee(p, archive.CategoryId, archive.SecrecyLevel);
                // 或：本人有已批准的借阅（未逾期）也可访问
                if (!allowed && p.UserId.HasValue)
                {
                    allowed = await _db.Select<BorrowRequest>()
                        .Where(b => b.ApplicantId == p.UserId.Value && b.TargetId == archive.Id
                                 && b.TargetType == "archive"
                                 && (b.Status == BorrowStatus.Approved)
                                 && (b.ExpireAt == null || b.ExpireAt > DateTime.UtcNow))
                        .AnyAsync();
                }
                if (!allowed)
                    throw new UnauthorizedAccessException("无权查看该附件：既无门类查看权限，也无有效借阅授权");
            }
        }

        var stream = await _storage.GetAsync(e.FileName);

        // 服务端水印：图片类附件叠加「利用者 + 时间」平铺水印，防截屏外传
        if (stream != null && _watermark.Supported(e.OriginalName))
        {
            var buf = await ReadAllAsync(stream);
            var marked = _watermark.Apply(buf, e.OriginalName, new[]
            {
                username ?? "anonymous",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
            if (!ReferenceEquals(marked, buf)) stream = new MemoryStream(marked);
        }

        await _audit.LogAsync("attachment.download", "Attachment", id, $"下载/预览附件 {e.OriginalName}");
        return (stream, ToDto(e));
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var e = await _db.Select<Attachment>().Where(a => a.Id == id).FirstAsync();
        if (e == null) return false;
        await _storage.DeleteAsync(e.FileName);
        var ok = await _db.Delete<Attachment>().Where(a => a.Id == id).ExecuteAffrowsAsync() > 0;
        if (ok) await _audit.LogAsync("attachment.delete", "Attachment", id, $"删除附件 {e.OriginalName}");
        return ok;
    }

    private static async Task<byte[]> ReadAllAsync(Stream s)
    {
        if (s is MemoryStream ms && ms.TryGetBuffer(out _)) return ms.ToArray();
        using var copy = new MemoryStream();
        await s.CopyToAsync(copy);
        s.Dispose();
        return copy.ToArray();
    }

    private static AttachmentDto ToDto(Attachment a) => new()
    {
        Id = a.Id, FileItemId = a.FileItemId, FileName = a.FileName, OriginalName = a.OriginalName,
        MimeType = a.MimeType, SizeBytes = a.SizeBytes, Hash = a.Hash, Version = a.Version,
        WatermarkFlag = a.WatermarkFlag, UploadedAt = a.UploadedAt
    };
}
