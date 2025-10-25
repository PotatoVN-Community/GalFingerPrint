using System;
using System.Linq;
using GalFingerPrint.Server.Models;
using GalFingerPrint.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace GalFingerPrint.Server.Controllers;

[ApiController]
[Route("game")]
public class GameController(IQueryHashService queryHashService) : ControllerBase
{
    /// <summary>根据文件哈希列表推断最可能的游戏</summary>
    /// <remarks>对每个哈希挑选计数最高的游戏，再从所有命中的游戏中选出出现次数最多且票数最高者作为结果</remarks>
    /// <response code="200">成功返回匹配到的游戏</response>
    /// <response code="400">缺少有效的哈希输入</response>
    /// <response code="404">没有找到匹配的游戏</response>
    [HttpPost("byhash")]
    public async Task<ActionResult<QueryHashResultDto>> QueryByHash([FromBody] QueryHashDto? request, CancellationToken ct)
    {
        if (request?.Hashes == null)
            return BadRequest("缺少 hashes");

        var sanitized = request.Hashes
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (sanitized.Count == 0)
            return BadRequest("缺少有效的 hashes");

        var result = await queryHashService.QueryBestMatchAsync(new QueryHashDto { Hashes = sanitized }, ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }
}
