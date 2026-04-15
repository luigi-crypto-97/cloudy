using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FriendMap.Api.Services;

public class ModerationService
{
    private readonly AppDbContext _db;

    public ModerationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ModerationQueueItemDto>> GetOpenQueueAsync(CancellationToken ct)
    {
        return await _db.ModerationReports
            .Where(x => x.Status == "open")
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new ModerationQueueItemDto(
                x.Id,
                x.CreatedAtUtc,
                x.ReasonCode,
                x.Status,
                x.ReportedUserId,
                x.ReportedVenueId,
                x.ReportedSocialTableId))
            .ToListAsync(ct);
    }

    public async Task<bool> ResolveAsync(Guid reportId, CancellationToken ct)
    {
        var report = await _db.ModerationReports.FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null) return false;

        report.Status = "resolved";
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
