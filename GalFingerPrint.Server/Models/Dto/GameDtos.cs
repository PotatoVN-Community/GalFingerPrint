namespace GalFingerPrint.Server.Models;

public class GameHashVoteDto
{
    public string Hash { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool IsDominant { get; set; }
}

public class GameVotesDto
{
    public string VndbId { get; set; } = string.Empty;
    public List<GameHashVoteDto> Items { get; set; } = new();
}

public class GameListDto
{
    public List<GalgameDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}