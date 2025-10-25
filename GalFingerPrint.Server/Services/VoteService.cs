using System.Net;
using GalFingerPrint.Server.Models;
using GalFingerPrint.Server.Repositories;
using GalFingerPrint.Server.Data;

namespace GalFingerPrint.Server.Services;

public class VoteService(
    IGalgameRepository galgameRepo,
    IHashRepository hashRepo,
    IGalgameHashRepository galgameHashRepo,
    IVoteRepository voteRepo,
    GalDbContext db) : IVoteService
{
    public async Task SubmitVoteAsync(string ip, VoteUpdateDto vote, CancellationToken ct = default)
    {
        var address = ParseIPv4ToUint(ip);
        var gal = await galgameRepo.GetOrCreateAsync(vote.VndbId, ct);

        var existing = await voteRepo.FindAsync(address, vote.VndbId, ct);

        var newSet = vote.Hashes.Distinct().ToHashSet(StringComparer.Ordinal);
        var oldSet = existing?.Hashes?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        // Short-circuit if exactly the same
        if (existing is not null && newSet.SetEquals(oldSet))
        {
            return;
        }

        var toDec = oldSet.Except(newSet).ToList();
        var toInc = newSet.Except(oldSet).ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Ensure hashes exist and get their ids
            var all = await hashRepo.GetOrCreateAsync(toDec.Concat(toInc), ct);
            var map = all.ToDictionary(h => h.HashValue, h => h.Id, StringComparer.Ordinal);

            foreach (var hv in toDec)
            {
                if (map.TryGetValue(hv, out var hid))
                    await galgameHashRepo.AddDeltaAsync(gal.Id, hid, -1, ct);
            }
            foreach (var hv in toInc)
            {
                if (map.TryGetValue(hv, out var hid))
                    await galgameHashRepo.AddDeltaAsync(gal.Id, hid, +1, ct);
            }

            // Upsert vote record
            var toSave = new Vote
            {
                Address = address,
                VndbId = vote.VndbId,
                Hashes = newSet.OrderBy(s => s, StringComparer.Ordinal).ToList()
            };
            await voteRepo.UpsertAsync(toSave, ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<(List<VoteDto> Items, int Total)> QueryVotesAsync(string ip, int page, int pageSize, CancellationToken ct = default)
    {
        var address = ParseIPv4ToUint(ip);
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
        var items = await voteRepo.GetByAddressAsync(address, page, pageSize, ct);
        var total = await voteRepo.CountByAddressAsync(address, ct);
        return (items.Select(v => new VoteDto { Id = v.Id, VndbId = v.VndbId, Hashes = v.Hashes }).ToList(), total);
    }

    private static uint ParseIPv4ToUint(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr) || addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("只支持 IPv4 地址", nameof(ip));
        var bytes = addr.GetAddressBytes();
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }
}
