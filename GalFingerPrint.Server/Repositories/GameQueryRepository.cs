using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GalFingerPrint.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GalFingerPrint.Server.Repositories;

public class GameQueryRepository(GalDbContext db) : IGameQueryRepository
{
    public async Task<List<GameHashCountRow>> GetHashMatchesAsync(IEnumerable<string> hashValues, CancellationToken ct = default)
    {
        var list = hashValues?
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? new List<string>();

        if (list.Count == 0)
            return new List<GameHashCountRow>();

        var rows = await (
            from gh in db.GalgameHashes.AsNoTracking()
            join h in db.Hashes.AsNoTracking() on gh.HashId equals h.Id
            join g in db.Galgames.AsNoTracking() on gh.GalgameId equals g.Id
            where list.Contains(h.HashValue)
            select new { h.HashValue, g.VndbId, gh.Cnt }
        ).ToListAsync(ct);

        return rows
            .Select(r => new GameHashCountRow(r.HashValue, r.VndbId, r.Cnt))
            .ToList();
    }
}
