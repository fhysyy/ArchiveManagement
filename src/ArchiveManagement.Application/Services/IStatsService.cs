using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Domain.Enums;
using FreeSql;

namespace ArchiveManagement.Application.Services;

public interface IStatsService
{
    Task<StatsOverviewDto> OverviewAsync();
    Task<BorrowStatsDto> BorrowAsync();
    Task<DueWarningDto> DueWarningAsync();
}

public class StatsService : IStatsService
{
    private readonly IFreeSql _db;
    public StatsService(IFreeSql db) => _db = db;

    public async Task<StatsOverviewDto> OverviewAsync()
    {
        var archives = await _db.Select<Archive>().ToListAsync();
        var cats = await _db.Select<ArchiveCategory>().ToListAsync();
        var nameOf = cats.ToDictionary(c => c.Id, c => c.Name);

        var dto = new StatsOverviewDto
        {
            Total = archives.Count,
            Archived = archives.Count(a => a.Status == ArchiveStatus.Archived),
            Draft = archives.Count(a => a.Status == ArchiveStatus.Draft)
        };
        dto.ByCategory = cats.Select(c => new CategoryStatDto
        {
            CategoryId = c.Id,
            CategoryName = c.Name,
            Count = archives.Count(a => a.CategoryId == c.Id)
        }).ToList();
        dto.ByYear = archives.Where(a => a.Year.HasValue)
            .GroupBy(a => a.Year!.Value).OrderBy(g => g.Key)
            .Select(g => new KvDto { Key = g.Key.ToString(), Count = g.Count() }).ToList();
        dto.ByRetention = archives.Where(a => !string.IsNullOrWhiteSpace(a.Retention))
            .GroupBy(a => a.Retention!).OrderBy(g => g.Key)
            .Select(g => new KvDto { Key = g.Key, Count = g.Count() }).ToList();
        dto.BySecrecy = archives.Where(a => !string.IsNullOrWhiteSpace(a.SecrecyLevel))
            .GroupBy(a => a.SecrecyLevel!).OrderBy(g => g.Key)
            .Select(g => new KvDto { Key = g.Key, Count = g.Count() }).ToList();
        return dto;
    }

    public async Task<BorrowStatsDto> BorrowAsync()
    {
        var borrows = await _db.Select<BorrowRequest>().ToListAsync();
        var targets = await _db.Select<Archive>().ToListAsync();
        var fileTargets = await _db.Select<FileItem>().ToListAsync();
        var users = await _db.Select<SysUser>().ToListAsync();
        var nameOf = users.ToDictionary(u => u.Id, u => u.Name ?? u.Username);

        var dto = new BorrowStatsDto
        {
            TotalBorrows = borrows.Count,
            PendingBorrows = borrows.Count(b => b.Status == BorrowStatus.Pending)
        };
        dto.ByTarget = borrows.GroupBy(b => new { b.TargetType, b.TargetId })
            .Select(g => new BorrowTargetStat
            {
                TargetType = g.Key.TargetType,
                TargetId = g.Key.TargetId,
                TargetTitle = g.Key.TargetType == "archive"
                    ? (targets.FirstOrDefault(t => t.Id == g.Key.TargetId)?.Title ?? $"档案#{g.Key.TargetId}")
                    : (fileTargets.FirstOrDefault(t => t.Id == g.Key.TargetId)?.Title ?? $"文件#{g.Key.TargetId}"),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count).Take(10).ToList();
        dto.ByApplicant = borrows.GroupBy(b => b.ApplicantId)
            .Select(g => new BorrowApplicantStat
            {
                ApplicantId = g.Key,
                ApplicantName = nameOf.TryGetValue(g.Key, out var n) ? n : $"用户#{g.Key}",
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count).Take(10).ToList();
        return dto;
    }

    public async Task<DueWarningDto> DueWarningAsync()
    {
        var curYear = DateTime.UtcNow.Year;
        var archives = await _db.Select<Archive>().ToListAsync();
        var appraisedIds = (await _db.Select<Appraisal>().ToListAsync()).Select(a => a.TargetId).ToHashSet();
        var items = new List<DueWarningItem>();
        foreach (var a in archives)
        {
            var years = ParseRetentionYears(a.Retention);
            if (years <= 0) continue;
            var baseYear = a.Year ?? curYear;
            var expiry = baseYear + years;
            if (expiry <= curYear)
                items.Add(new DueWarningItem
                {
                    Archive = ToArchiveDto(a),
                    ExpiryYear = expiry,
                    Appraised = appraisedIds.Contains(a.Id)
                });
        }
        return new DueWarningDto { Total = items.Count, Items = items };
    }

    private static ArchiveDto ToArchiveDto(Archive a) => new()
    {
        Id = a.Id, CategoryId = a.CategoryId, ParentId = a.ParentId, IsProject = a.IsProject,
        ArchiveNo = a.ArchiveNo, Title = a.Title, Year = a.Year, Retention = a.Retention,
        SecrecyLevel = a.SecrecyLevel, OrgUnit = a.OrgUnit, DynamicFields = a.DynamicFields,
        Status = a.Status.ToString()
    };

    private static int ParseRetentionYears(string? retention)
    {
        if (string.IsNullOrWhiteSpace(retention)) return 0;
        if (retention.Contains("永久")) return -1;
        var m = System.Text.RegularExpressions.Regex.Match(retention, @"(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }
}
