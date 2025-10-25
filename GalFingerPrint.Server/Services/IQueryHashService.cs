using System.Threading;
using System.Threading.Tasks;
using GalFingerPrint.Server.Models;

namespace GalFingerPrint.Server.Services;

public interface IQueryHashService
{
    Task<QueryHashResultDto?> QueryBestMatchAsync(QueryHashDto request, CancellationToken ct = default);
}
