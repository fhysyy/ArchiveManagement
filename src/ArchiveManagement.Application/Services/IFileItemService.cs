using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface IFileItemService
{
    Task<List<FileItemDto>> ListByArchiveAsync(long archiveId);
    Task<FileItemDto?> GetAsync(long id);
    Task<FileItemDto> CreateAsync(CreateFileItemDto dto);
    Task<FileItemDto?> UpdateAsync(long id, CreateFileItemDto dto);
    Task<bool> DeleteAsync(long id);
}

public class FileItemService : IFileItemService
{
    private readonly IFreeSql _db;
    public FileItemService(IFreeSql db) => _db = db;

    public async Task<List<FileItemDto>> ListByArchiveAsync(long archiveId)
    {
        var list = await _db.Select<FileItem>()
            .Where(f => f.ArchiveId == archiveId)
            .OrderBy(f => f.SeqNo ?? 0).OrderByDescending(f => f.Id)
            .ToListAsync();
        return list.Select(ToDto).ToList()!;
    }

    public async Task<FileItemDto?> GetAsync(long id)
        => ToDto(await _db.Select<FileItem>().Where(f => f.Id == id).FirstAsync());

    public async Task<FileItemDto> CreateAsync(CreateFileItemDto dto)
    {
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
        return ToDto(e)!;
    }

    public async Task<FileItemDto?> UpdateAsync(long id, CreateFileItemDto dto)
    {
        var e = await _db.Select<FileItem>().Where(f => f.Id == id).FirstAsync();
        if (e == null) return null;
        e.Title = dto.Title;
        e.DocDate = dto.DocDate;
        e.Author = dto.Author;
        e.Pages = dto.Pages;
        e.SeqNo = dto.SeqNo;
        e.FileNo = dto.FileNo;
        e.ProjectNo = dto.ProjectNo;
        e.DynamicFields = dto.DynamicFields;
        await _db.Update<FileItem>().SetSource(e).ExecuteAffrowsAsync();
        return ToDto(e);
    }

    public async Task<bool> DeleteAsync(long id)
        => await _db.Delete<FileItem>().Where(f => f.Id == id).ExecuteAffrowsAsync() > 0;

    private static FileItemDto ToDto(FileItem f) => new()
    {
        Id = f.Id, ArchiveId = f.ArchiveId, Title = f.Title, DocDate = f.DocDate,
        Author = f.Author, Pages = f.Pages, SeqNo = f.SeqNo, FileNo = f.FileNo,
        ProjectNo = f.ProjectNo, DynamicFields = f.DynamicFields, CreatedAt = f.CreatedAt
    };
}
