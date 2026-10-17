using ArchiveManagement.Domain.Entities;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveManagement.Infrastructure;

public static class FreeSqlSetup
{
    public static IServiceCollection AddArchiveDb(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("SqlServer")
                   ?? "Server=localhost;Database=ArchiveManagement;Trusted_Connection=True;TrustServerCertificate=True;";

        // 库不存在则自动创建（使用 master 连接执行 CREATE DATABASE）
        try
        {
            EnsureDatabaseAsync(conn).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[ArchiveDb] 自动建库失败（请检查数据库连接/权限）：{ex.Message}");
            Console.ResetColor();
        }

        var fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.SqlServer, conn)
            .UseAutoSyncStructure(true)
            .Build();

        try
        {
            fsql.CodeFirst.SyncStructure(
                typeof(Fonds), typeof(ArchiveCategory), typeof(Archive),
                typeof(FileItem), typeof(Attachment),
                typeof(SysUser), typeof(SysRole), typeof(SysUserRole), typeof(SysPermission),
                typeof(BorrowRequest), typeof(Appraisal), typeof(AuditLog));

            DbSeeder.Seed(fsql);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[ArchiveDb] 建表/种子失败（请检查数据库连接/权限）：{ex.Message}");
            Console.ResetColor();
        }

        services.AddSingleton<IFreeSql>(fsql);

        // 未配置时回退到持久目录（ProgramData），不要落在 AppContext.BaseDirectory（bin）里，避免重新发布后附件丢失
        var storageRoot = config["Storage:LocalPath"];
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            storageRoot = Path.Combine(
                string.IsNullOrWhiteSpace(programData) ? "C:\\ProgramData" : programData,
                "ArchiveManagement", "uploads");
        }
        services.AddSingleton<IObjectStorage>(new LocalObjectStorage(storageRoot));
        return services;
    }

    private static async Task EnsureDatabaseAsync(string conn)
    {
        var dbName = ExtractDatabase(conn);
        var masterConn = ToMaster(conn);
        var master = new FreeSqlBuilder()
            .UseConnectionString(DataType.SqlServer, masterConn)
            .Build();
        try
        {
            var exists = Convert.ToInt32(await master.Ado.ExecuteScalarAsync(
                "SELECT COUNT(1) FROM sys.databases WHERE name = @n", new { n = dbName }));
            if (exists == 0)
                await master.Ado.ExecuteNonQueryAsync($"CREATE DATABASE [{dbName}]");
        }
        finally
        {
            master.Dispose();
        }
    }

    private static string ExtractDatabase(string conn)
    {
        foreach (var p in conn.Split(';'))
        {
            var t = p.Trim();
            if (t.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                return t.Substring("Database=".Length).Trim();
            if (t.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
                return t.Substring("Initial Catalog=".Length).Trim();
        }
        return "ArchiveManagement";
    }

    private static string ToMaster(string conn)
    {
        var parts = conn.Split(';');
        for (var i = 0; i < parts.Length; i++)
        {
            var t = parts[i].Trim();
            if (t.StartsWith("Database=", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
                parts[i] = "Database=master";
        }
        return string.Join(";", parts);
    }
}
