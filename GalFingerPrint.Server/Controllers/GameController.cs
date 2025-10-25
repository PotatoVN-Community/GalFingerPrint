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
