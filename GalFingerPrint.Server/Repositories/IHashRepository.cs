using GalFingerPrint.Server.Models;

namespace GalFingerPrint.Server.Repositories;

public interface IHashRepository
{
    Task<Hash?> FindByValueAsync(string hashValue, CancellationToken ct = default);
    Task<Hash> GetOrCreateAsync(string hashValue, CancellationToken ct = default);
    Task<List<Hash>> GetOrCreateAsync(IEnumerable<string> hashValues, CancellationToken ct = default);
}

