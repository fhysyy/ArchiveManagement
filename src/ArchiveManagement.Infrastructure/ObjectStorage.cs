namespace ArchiveManagement.Infrastructure;

/// <summary>对象存储抽象：开发期用本地磁盘，生产可换 MinIO / 云 OSS</summary>
public interface IObjectStorage
{
    Task<string> SaveAsync(Stream stream, string fileName);
    Task<Stream?> GetAsync(string key);
    Task DeleteAsync(string key);
}

public class LocalObjectStorage : IObjectStorage
{
    private readonly string _root;
    public LocalObjectStorage(string root) => _root = root;

    public async Task<string> SaveAsync(Stream stream, string fileName)
    {
        var key = $"{Guid.NewGuid():N}_{fileName}";
        var dir = Path.Combine(_root, DateTime.UtcNow.ToString("yyyyMM"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, key);
        await using var fs = File.Create(path);
        await stream.CopyToAsync(fs);
        return path; // 存 DB 的 Attachment.FileName 即此路径/key
    }

    public async Task<Stream?> GetAsync(string key)
        => File.Exists(key) ? File.OpenRead(key) : null;

    public Task DeleteAsync(string key)
    {
        if (File.Exists(key)) File.Delete(key);
        return Task.CompletedTask;
    }
}
