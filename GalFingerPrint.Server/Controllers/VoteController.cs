using GalFingerPrint.Server.Helper;
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

    /// <summary>提交或更新当前 IP 对指定游戏的哈希投票</summary>
    /// <remarks>会根据新的哈希集合自动增减数据库中记录的计数，确保每个 IP 对每个游戏仅保留一份投票</remarks>
    /// <response code="204">投票提交成功</response>
    /// <response code="400">请求体缺少有效的 hashes</response>
    [HttpPatch("{vndbId}")]
    public async Task<IActionResult> PatchVote([FromRoute] string vndbId, [FromBody] VotePatchRequest req, CancellationToken ct)
    {
        if (req?.Hashes is null) return BadRequest("缺少 hashes");
        var ip = this.GetClientIp();
        var dto = new VoteUpdateDto { VndbId = vndbId, Hashes = req.Hashes };
        await voteService.SubmitVoteAsync(ip, dto, ct);
        return NoContent();
    }

    /// <summary>分页查询当前 IP 的投票记录</summary>
    /// <remarks>根据请求来源 IP 地址返回用户曾经提交的游戏哈希投票记录</remarks>
    /// <response code="200">返回投票分页数据</response>
    [HttpGet]
    public async Task<ActionResult<VoteQueryResponseDto>> GetVotes([FromQuery] int page = 1,
        [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var ip = this.GetClientIp();
        var (items, total) = await voteService.QueryVotesAsync(ip, page, pageSize, ct);
        var response = new VoteQueryResponseDto
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
            Ip = ip
        };
        return Ok(response);
    }
}
