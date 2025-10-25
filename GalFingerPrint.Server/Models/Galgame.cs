namespace GalFingerPrint.Server.Models;

public class Galgame
{
    public int Id { get; set; }
    public required string VndbId { get; set; }
    
    public List<GalgameHash> GalgameHashes { get; set; } = [];
}

public class GalgameDto
{
    public required string VndbId { get; set; }
}