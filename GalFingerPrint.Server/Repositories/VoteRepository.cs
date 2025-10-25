using GalFingerPrint.Server.Data;
using GalFingerPrint.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalFingerPrint.Server.Repositories;

public class VoteRepository(GalDbContext db) : IVoteRepository
{
    public async Task<Vote?> FindAsync(uint address, string vndbId, CancellationToken ct = default)
        => await db.Votes.FirstOrDefaultAsync(v => v.Address == address && v.VndbId == vndbId, ct);

    public async Task<Vote> UpsertAsync(Vote vote, CancellationToken ct = default)
    {
        var existing = await db.Votes.FirstOrDefaultAsync(v => v.Address == vote.Address && v.VndbId == vote.VndbId, ct);
        if (existing is null)
        {
            db.Votes.Add(vote);
        }
        else
        {
            existing.Hashes = vote.Hashes;
        }
        await db.SaveChangesAsync(ct);
        return existing ?? vote;
    }

    public async Task<List<Vote>> GetByAddressAsync(uint address, int page, int pageSize, CancellationToken ct = default)
        => await db.Votes
            .AsNoTracking()
            .Where(v => v.Address == address)
            .OrderBy(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<int> CountByAddressAsync(uint address, CancellationToken ct = default)
        => await db.Votes.CountAsync(v => v.Address == address, ct);
}

