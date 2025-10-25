using GalFingerPrint.Server.Data;
using GalFingerPrint.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalFingerPrint.Server.Repositories;

public class GalgameHashRepository(GalDbContext db) : IGalgameHashRepository
{
    public async Task AddDeltaAsync(int galgameId, int hashId, int delta, CancellationToken ct = default)
    {
        // Lock the row if exists to avoid race; otherwise try insert
        var entity = await db.GalgameHashes
            .FirstOrDefaultAsync(x => x.GalgameId == galgameId && x.HashId == hashId, ct);

        if (entity is null)
        {
            if (delta <= 0) return; // nothing to decrement
            entity = new GalgameHash { GalgameId = galgameId, HashId = hashId, Cnt = delta };
            db.GalgameHashes.Add(entity);
        }
        else
        {
            entity.Cnt += delta;
            if (entity.Cnt <= 0)
            {
                db.GalgameHashes.Remove(entity);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}

