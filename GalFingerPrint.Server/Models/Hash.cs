namespace GalFingerPrint.Server.Models;

public class Hash
{
    public int Id { get; set; }
    public required string HashValue { get; set; }
    
    public List<GalgameHash> GalgameHashes { get; set; } = [];
}