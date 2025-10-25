using GalFingerPrint.Server.Data;
using GalFingerPrint.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalFingerPrint.Server.Repositories;

public class GalgameRepository(GalDbContext db) : IGalgameRepository
{
    public async Task<Galgame?> FindByVndbIdAsync(string vndbId, CancellationToken ct = default)
        => await db.Galgames.AsNoTracking().FirstOrDefaultAsync(x => x.VndbId == vndbId, ct);

    public async Task<Galgame> GetOrCreateAsync(string vndbId, CancellationToken ct = default)
    {
        var entity = await db.Galgames.FirstOrDefaultAsync(x => x.VndbId == vndbId, ct);
        if (entity is not null) return entity;
        entity = new Galgame { VndbId = vndbId };
        db.Galgames.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }
}

