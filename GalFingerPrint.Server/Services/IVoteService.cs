using GalFingerPrint.Server.Models;

namespace GalFingerPrint.Server.Services;

public interface IVoteService
{
    Task SubmitVoteAsync(string ip, VoteUpdateDto vote, CancellationToken ct = default);
    Task<(List<VoteDto> Items, int Total)> QueryVotesAsync(string ip, int page, int pageSize, CancellationToken ct = default);
}

