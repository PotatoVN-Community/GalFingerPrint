namespace GalFingerPrint.Server.Models;

public class GalgameHash
{
    public int HashId { get; set; }
    public int GalgameId { get; set; }
    /// <summary>
    /// 被投票次数
    /// </summary>
    public int Cnt { get; set; }
}