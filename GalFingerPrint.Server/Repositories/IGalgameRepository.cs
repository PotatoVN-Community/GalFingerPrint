using GalFingerPrint.Server.Models;

namespace GalFingerPrint.Server.Repositories;

public interface IGalgameRepository
{
    Task<Galgame?> FindByVndbIdAsync(string vndbId, CancellationToken ct = default);
    Task<Galgame> GetOrCreateAsync(string vndbId, CancellationToken ct = default);
}

