using GalFingerPrint.Server.Models;
using GalFingerPrint.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GalFingerPrint.Server.Services;

public interface IGameService
{
    Task<GameVotesDto?> GetGameVotesAsync(string vndbId, CancellationToken ct = default);
    Task<GameListDto> ListGamesAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
}

public class GameService(GalDbContext db) : IGameService
{
    public async Task<GameVotesDto?> GetGameVotesAsync(string vndbId, CancellationToken ct = default)
    {
        var gal = await db.Galgames.AsNoTracking().FirstOrDefaultAsync(g => g.VndbId == vndbId, ct);
        if (gal is null)
            return null;

        var rows = await (
            from gh in db.GalgameHashes.AsNoTracking()
            join h in db.Hashes.AsNoTracking() on gh.HashId equals h.Id
            where gh.GalgameId == gal.Id
            select new { h.HashValue, gh.Cnt }
        ).ToListAsync(ct);

        if (rows.Count == 0)
            return new GameVotesDto { VndbId = vndbId, Items = new List<GameHashVoteDto>() };

        var hashValues = rows.Select(r => r.HashValue).Distinct(StringComparer.Ordinal).ToList();

        var maxs = await (
            from gh in db.GalgameHashes.AsNoTracking()
            join h in db.Hashes.AsNoTracking() on gh.HashId equals h.Id
            where hashValues.Contains(h.HashValue)
            group gh by h.HashValue into g
            select new { HashValue = g.Key, Max = g.Max(x => x.Cnt) }
        ).ToListAsync(ct);

        var maxMap = maxs.ToDictionary(x => x.HashValue, x => x.Max, StringComparer.Ordinal);

        var items = rows
            .Select(r => new GameHashVoteDto
            {
                Hash = r.HashValue,
                Count = r.Cnt,
                IsDominant = maxMap.TryGetValue(r.HashValue, out var m) && m > 0 && r.Cnt == m
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Hash, StringComparer.Ordinal)
            .ToList();

        return new GameVotesDto { VndbId = vndbId, Items = items };
    }

    public async Task<GameListDto> ListGamesAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;

        var total = await db.Galgames.AsNoTracking().CountAsync(ct);
        var items = await db.Galgames.AsNoTracking()
            .OrderBy(g => g.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GalgameDto { VndbId = g.VndbId })
            .ToListAsync(ct);

        return new GameListDto
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }
}