using GalFingerPrint.Server.Models;
using GalFingerPrint.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace GalFingerPrint.Server.Controllers;

[ApiController]
[Route("vote")]
public class VoteController(IVoteService voteService) : ControllerBase
{
    public class VotePatchRequest
    {
        public List<string> Hashes { get; set; } = [];
    }

    private string GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip;
    }

    [HttpPatch("{vndbId}")]
    public async Task<IActionResult> PatchVote([FromRoute] string vndbId, [FromBody] VotePatchRequest req, CancellationToken ct)
    {
        if (req?.Hashes is null) return BadRequest("缺少 hashes");
        var ip = GetClientIp();
        var dto = new VoteUpdateDto { VndbId = vndbId, Hashes = req.Hashes };
        await voteService.SubmitVoteAsync(ip, dto, ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetVotes([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var ip = GetClientIp();
        var (items, total) = await voteService.QueryVotesAsync(ip, page, pageSize, ct);
        return Ok(new { items, total, page, pageSize });
    }
}
