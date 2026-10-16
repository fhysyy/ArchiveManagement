using ArchiveManagement.Application.Dtos;
using ArchiveManagement.Domain.Entities;
using ArchiveManagement.Domain.Enums;

namespace ArchiveManagement.Application.Services;

public interface IBorrowService
{
    Task<BorrowDto> CreateAsync(CreateBorrowDto dto, string? applicantName);
    Task<List<BorrowDto>> ListAsync(string role, string? viewerName);
    Task<BorrowDto?> ApproveAsync(long id, string? approverName, BorrowAuditDto? dto);
    Task<BorrowDto?> RejectAsync(long id, string? approverName, BorrowAuditDto? dto);
    Task<BorrowDto?> ReturnAsync(long id);
    Task<BorrowDto?> RenewAsync(long id, int days);
}

public class BorrowService : IBorrowService
{
    private readonly IFreeSql _db;
    public BorrowService(IFreeSql db) => _db = db;

    private async Task<long?> ResolveUserId(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        var u = await _db.Select<SysUser>().Where(x => x.Username == username).FirstAsync();
        return u?.Id;
    }

    public async Task<BorrowDto> CreateAsync(CreateBorrowDto dto, string? applicantName)
    {
        var applicantId = await ResolveUserId(applicantName);
        var e = new BorrowRequest
        {
            ApplicantId = applicantId ?? 0,
            TargetType = dto.TargetType,
            TargetId = dto.TargetId,
            Days = dto.Days,
            Reason = dto.Reason,
            Status = BorrowStatus.Pending
        };
        e.Id = await _db.Insert(e).ExecuteIdentityAsync();
        return await ToDto(e);
    }

    public async Task<List<BorrowDto>> ListAsync(string role, string? viewerName)
    {
        var q = _db.Select<BorrowRequest>();
        if (string.Equals(role, "applicant", StringComparison.OrdinalIgnoreCase))
        {
            var uid = await ResolveUserId(viewerName);
            q = q.Where(b => b.ApplicantId == (uid ?? 0));
        }
        else if (string.Equals(role, "approver", StringComparison.OrdinalIgnoreCase))
        {
            q = q.Where(b => b.Status == BorrowStatus.Pending);
        }
        // 其它(含 "all"/admin) 返回全部
        var list = await q.OrderByDescending(b => b.Id).ToListAsync();
        var dtos = new List<BorrowDto>();
        foreach (var b in list) dtos.Add(await ToDto(b));
        return dtos;
    }

    public async Task<BorrowDto?> ApproveAsync(long id, string? approverName, BorrowAuditDto? dto)
    {
        var e = await _db.Select<BorrowRequest>().Where(b => b.Id == id).FirstAsync();
        if (e == null) return null;
        var approverId = await ResolveUserId(approverName);
        int days = dto?.Days ?? e.Days ?? 7;
        e.Status = BorrowStatus.Approved;
        e.ApprovedBy = approverId;
        e.ApprovedAt = DateTime.UtcNow;
        e.ExpireAt = DateTime.UtcNow.AddDays(days);
        await _db.Update<BorrowRequest>().SetSource(e).ExecuteAffrowsAsync();
        return await ToDto(e);
    }

    public async Task<BorrowDto?> RejectAsync(long id, string? approverName, BorrowAuditDto? dto)
    {
        var e = await _db.Select<BorrowRequest>().Where(b => b.Id == id).FirstAsync();
        if (e == null) return null;
        e.Status = BorrowStatus.Rejected;
        e.ApprovedBy = await ResolveUserId(approverName);
        e.ApprovedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto?.Comment))
            e.Reason = string.IsNullOrWhiteSpace(e.Reason) ? $"[驳回]{dto.Comment}" : $"{e.Reason}\n[驳回]{dto.Comment}";
        await _db.Update<BorrowRequest>().SetSource(e).ExecuteAffrowsAsync();
        return await ToDto(e);
    }

    public async Task<BorrowDto?> ReturnAsync(long id)
    {
        var e = await _db.Select<BorrowRequest>().Where(b => b.Id == id).FirstAsync();
        if (e == null) return null;
        e.Status = BorrowStatus.Returned;
        await _db.Update<BorrowRequest>().SetSource(e).ExecuteAffrowsAsync();
        return await ToDto(e);
    }

    public async Task<BorrowDto?> RenewAsync(long id, int days)
    {
        var e = await _db.Select<BorrowRequest>().Where(b => b.Id == id).FirstAsync();
        if (e == null) return null;
        if (e.Status != BorrowStatus.Approved)
            throw new InvalidOperationException("仅已批准的借阅可续期");
        var add = days <= 0 ? 7 : days;
        e.ExpireAt = (e.ExpireAt ?? DateTime.UtcNow).AddDays(add);
        e.Days = (e.Days ?? 0) + add;
        await _db.Update<BorrowRequest>().SetSource(e).ExecuteAffrowsAsync();
        return await ToDto(e);
    }

    private async Task<BorrowDto> ToDto(BorrowRequest b)
    {
        var applicant = await _db.Select<SysUser>().Where(u => u.Id == b.ApplicantId).FirstAsync();
        var approver = b.ApprovedBy.HasValue
            ? await _db.Select<SysUser>().Where(u => u.Id == b.ApprovedBy.Value).FirstAsync()
            : null;
        string targetTitle = string.Empty;
        if (b.TargetType == "archive")
        {
            var a = await _db.Select<Archive>().Where(x => x.Id == b.TargetId).FirstAsync();
            targetTitle = a?.Title ?? $"档案#{b.TargetId}";
        }
        else
        {
            var f = await _db.Select<FileItem>().Where(x => x.Id == b.TargetId).FirstAsync();
            targetTitle = f?.Title ?? $"文件#{b.TargetId}";
        }
        return new BorrowDto
        {
            Id = b.Id, ApplicantId = b.ApplicantId, ApplicantName = applicant?.Name ?? applicant?.Username ?? "",
            TargetType = b.TargetType, TargetId = b.TargetId, TargetTitle = targetTitle,
            Days = b.Days, Reason = b.Reason, Status = b.Status,
            ApprovedById = b.ApprovedBy, ApprovedByName = approver?.Name ?? approver?.Username ?? "",
            ApprovedAt = b.ApprovedAt, ExpireAt = b.ExpireAt, CreatedAt = b.CreatedAt
        };
    }
}
