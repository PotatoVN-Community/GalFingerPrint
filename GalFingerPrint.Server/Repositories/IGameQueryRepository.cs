using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GalFingerPrint.Server.Repositories;

public interface IGameQueryRepository
{
    Task<List<GameHashCountRow>> GetHashMatchesAsync(IEnumerable<string> hashValues, CancellationToken ct = default);
}

public record GameHashCountRow(string HashValue, string VndbId, int Count);
