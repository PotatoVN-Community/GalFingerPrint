using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GalFingerPrint.Server.Models;
using GalFingerPrint.Server.Repositories;

namespace GalFingerPrint.Server.Services;

public class QueryHashService(IGameQueryRepository repository) : IQueryHashService
{
    private sealed class Score
    {
        public int HitCount { get; set; }
        public int TotalCnt { get; set; }
    }

    public async Task<QueryHashResultDto?> QueryBestMatchAsync(QueryHashDto request, CancellationToken ct = default)
    {
        var hashes = request?.Hashes?
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? new List<string>();

        if (hashes.Count == 0)
            return null;

        var rows = await repository.GetHashMatchesAsync(hashes, ct);
        if (rows.Count == 0)
            return null;

        var scores = new Dictionary<string, Score>(StringComparer.Ordinal);

        foreach (var group in rows.GroupBy(r => r.HashValue, StringComparer.Ordinal))
        {
            var max = group.Max(r => r.Count);
            if (max <= 0)
                continue;

            foreach (var entry in group.Where(r => r.Count == max))
            {
                if (!scores.TryGetValue(entry.VndbId, out var score))
                {
                    score = new Score();
                    scores[entry.VndbId] = score;
                }
                score.HitCount += 1;
                score.TotalCnt += entry.Count;
            }
        }

        if (scores.Count == 0)
            return null;

        var winner = scores
            .OrderByDescending(kv => kv.Value.HitCount)
            .ThenByDescending(kv => kv.Value.TotalCnt)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .First();

        return new QueryHashResultDto { VndbId = winner.Key };
    }
}
