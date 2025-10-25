using GalFingerPrint.Server.Models;

namespace GalFingerPrint.Server.Repositories;

public interface IVoteRepository
{
    Task<Vote?> FindAsync(uint address, string vndbId, CancellationToken ct = default);
    Task<Vote> UpsertAsync(Vote vote, CancellationToken ct = default);
    Task<List<Vote>> GetByAddressAsync(uint address, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountByAddressAsync(uint address, CancellationToken ct = default);
}

