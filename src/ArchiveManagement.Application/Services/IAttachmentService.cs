using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Infrastructure;
using FreeSql;
using System.Security.Cryptography;

namespace ArchiveManagement.Application.Services;

public interface IAttachmentService
{
    Task<List<AttachmentDto>> ListByFileAsync(long fileItemId);
    Task<AttachmentUploadResultDto> UploadAsync(long fileItemId, string fileName, string? contentType, long size, Stream content, long? uploaderId);
    Task<(Stream? stream, AttachmentDto? meta)> DownloadAsync(long id);
    Task<bool> DeleteAsync(long id);
}

public class AttachmentService : IAttachmentService
{
    private readonly IFreeSql _db;
    private readonly IObjectStorage _storage;
    public AttachmentService(IFreeSql db, IObjectStorage storage) => (_db, _storage) = (db, storage);

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
        return new AttachmentUploadResultDto
        {
            Id = e.Id,
            OriginalName = e.OriginalName,
            Hash = e.Hash,
            SizeBytes = e.SizeBytes
        };
    }

    public async Task<(Stream? stream, AttachmentDto? meta)> DownloadAsync(long id)
    {
        var e = await _db.Select<Attachment>().Where(a => a.Id == id).FirstAsync();
        if (e == null) return (null, null);
        var stream = await _storage.GetAsync(e.FileName);
        return (stream, ToDto(e));
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var e = await _db.Select<Attachment>().Where(a => a.Id == id).FirstAsync();
        if (e == null) return false;
        await _storage.DeleteAsync(e.FileName);
        return await _db.Delete<Attachment>().Where(a => a.Id == id).ExecuteAffrowsAsync() > 0;
    }

    private static AttachmentDto ToDto(Attachment a) => new()
    {
        Id = a.Id, FileItemId = a.FileItemId, FileName = a.FileName, OriginalName = a.OriginalName,
        MimeType = a.MimeType, SizeBytes = a.SizeBytes, Hash = a.Hash, Version = a.Version,
        WatermarkFlag = a.WatermarkFlag, UploadedAt = a.UploadedAt
    };
}
