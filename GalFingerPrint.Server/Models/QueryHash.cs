namespace GalFingerPrint.Server.Models;

public class QueryHashDto
{
    public List<string> Hashes { get; set; } = [];
}

public class QueryHashResultDto
{
    public required string VndbId { get; set; }
}