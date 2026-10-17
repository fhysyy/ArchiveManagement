using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Domain.Enums;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface IFileItemService
{
    Task<List<FileItemDto>> ListByArchiveAsync(long archiveId);
    Task<FileItemDto?> GetAsync(long id);
    Task<FileItemDto> CreateAsync(CreateFileItemDto dto, string? username = null);
    Task<FileItemDto?> UpdateAsync(long id, CreateFileItemDto dto, string? username = null);
    Task<bool> DeleteAsync(long id, string? username = null);
}

public class FileItemService : IFileItemService
{
    private readonly IFreeSql _db;
    private readonly IPermissionService _perm;
    private readonly IAuditLogger _audit;
    private readonly IAttachmentService _att;

    public FileItemService(IFreeSql db, IPermissionService perm, IAuditLogger audit, IAttachmentService att)
        => (_db, _perm, _audit, _att) = (db, perm, audit, att);

    /// <summary>取父档案并校验：权限 + 已归档/已销毁不允许改动</summary>
    private async Task LoadEditableArchive(long archiveId, string? username, string op)
    {
        var a = await _db.Select<Archive>().Where(x => x.Id == archiveId && !x.IsDeleted).FirstAsync()
                ?? throw new InvalidOperationException("档案不存在");
        var p = await _perm.ResolveAsync(username);
        if (!_perm.Can(p, a.CategoryId, op))
            throw new UnauthorizedAccessException("当前角色无权操作该门类下的文件");
        if (a.Status == ArchiveStatus.Archived || a.Status == ArchiveStatus.Destroyed)
            throw new InvalidOperationException($"档案状态为 {a.Status}，不可改动其文件");
    }

    public async Task<List<FileItemDto>> ListByArchiveAsync(long archiveId)
    {
        var list = await _db.Select<FileItem>()
            .Where(f => f.ArchiveId == archiveId && !f.IsDeleted)
            .OrderBy(f => f.SeqNo ?? 0).OrderByDescending(f => f.Id)
            .ToListAsync();
        return list.Select(ToDto).ToList()!;
    }

    public async Task<FileItemDto?> GetAsync(long id)
        => ToDto(await _db.Select<FileItem>().Where(f => f.Id == id && !f.IsDeleted).FirstAsync());

    public async Task<FileItemDto> CreateAsync(CreateFileItemDto dto, string? username = null)
    {
        await LoadEditableArchive(dto.ArchiveId, username, "edit");
        var e = new FileItem
        {
            ArchiveId = dto.ArchiveId,
            Title = dto.Title,
            DocDate = dto.DocDate,
            Author = dto.Author,
            Pages = dto.Pages,
            SeqNo = dto.SeqNo,
            FileNo = dto.FileNo,
            ProjectNo = dto.ProjectNo,
            DynamicFields = dto.DynamicFields
        };
        e.Id = await _db.Insert(e).ExecuteIdentityAsync();
        await _audit.LogAsync("file.create", "FileItem", e.Id, $"新增文件 {e.Title}");
        return ToDto(e)!;
    }

    public async Task<FileItemDto?> UpdateAsync(long id, CreateFileItemDto dto, string? username = null)
    {
        var e = await _db.Select<FileItem>().Where(f => f.Id == id && !f.IsDeleted).FirstAsync();
        if (e == null) return null;
        await LoadEditableArchive(e.ArchiveId, username, "edit");

        e.Title = dto.Title;
        e.DocDate = dto.DocDate;
        e.Author = dto.Author;
        e.Pages = dto.Pages;
        e.SeqNo = dto.SeqNo;
        e.FileNo = dto.FileNo;
        e.ProjectNo = dto.ProjectNo;
        e.DynamicFields = dto.DynamicFields;
        await _db.Update<FileItem>().SetSource(e).ExecuteAffrowsAsync();
        await _audit.LogAsync("file.update", "FileItem", id, $"修改文件 {e.Title}");
        return ToDto(e);
    }

    /// <summary>逻辑删除文件，并级联删除其附件（含对象存储）</summary>
    public async Task<bool> DeleteAsync(long id, string? username = null)
    {
        var e = await _db.Select<FileItem>().Where(f => f.Id == id && !f.IsDeleted).FirstAsync();
        if (e == null) return false;
        await LoadEditableArchive(e.ArchiveId, username, "destroy");

        var atts = await _db.Select<Attachment>().Where(x => x.FileItemId == id).ToListAsync();
        foreach (var a in atts) await _att.DeleteAsync(a.Id);

        e.IsDeleted = true;
        e.DeletedAt = DateTime.UtcNow;
        var aff = await _db.Update<FileItem>().SetSource(e).ExecuteAffrowsAsync();
        await _audit.LogAsync("file.delete", "FileItem", id, $"删除文件 {e.Title}（含 {atts.Count} 个附件）");
        return aff > 0;
    }

    private static FileItemDto ToDto(FileItem f) => new()
    {
        Id = f.Id, ArchiveId = f.ArchiveId, Title = f.Title, DocDate = f.DocDate,
        Author = f.Author, Pages = f.Pages, SeqNo = f.SeqNo, FileNo = f.FileNo,
        ProjectNo = f.ProjectNo, DynamicFields = f.DynamicFields, CreatedAt = f.CreatedAt
    };
}
