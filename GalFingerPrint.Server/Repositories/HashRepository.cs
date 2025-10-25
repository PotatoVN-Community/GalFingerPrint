using GalFingerPrint.Server.Data;
using GalFingerPrint.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalFingerPrint.Server.Repositories;

public class HashRepository(GalDbContext db) : IHashRepository
{
    public async Task<Hash?> FindByValueAsync(string hashValue, CancellationToken ct = default)
        => await db.Hashes.AsNoTracking().FirstOrDefaultAsync(h => h.HashValue == hashValue, ct);

    public async Task<Hash> GetOrCreateAsync(string hashValue, CancellationToken ct = default)
    {
        var entity = await db.Hashes.FirstOrDefaultAsync(h => h.HashValue == hashValue, ct);
        if (entity is not null) return entity;
        entity = new Hash { HashValue = hashValue };
        db.Hashes.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<List<Hash>> GetOrCreateAsync(IEnumerable<string> hashValues, CancellationToken ct = default)
    {
        var list = hashValues.Distinct().ToList();
        var existing = await db.Hashes.Where(h => list.Contains(h.HashValue)).ToListAsync(ct);
        var toCreateValues = list.Except(existing.Select(e => e.HashValue)).ToList();
        foreach (var hv in toCreateValues)
        {
            db.Hashes.Add(new Hash { HashValue = hv });
        }
        if (toCreateValues.Count > 0)
            await db.SaveChangesAsync(ct);
        var all = await db.Hashes.Where(h => list.Contains(h.HashValue)).ToListAsync(ct);
        return all;
    }
}

