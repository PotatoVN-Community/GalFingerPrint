using System.ComponentModel.DataAnnotations;

namespace GalFingerPrint.Server.Models;

public class Vote
{
    public int Id { get; set; }
    /// 用户ip地址
    public required uint Address { get; set; }
    /// 游戏id
    [MaxLength(20)] public required string VndbId { get; set; }
    /// 该游戏文件的哈希列表
    public List<string> Hashes { get; set; } = [];
}

public class VoteUpdateDto
{
    public required string VndbId { get; set; }
    public List<string> Hashes { get; set; } = [];
}

public class VoteDto
{
    public int Id { get; set; }
    public string VndbId { get; set; } = string.Empty;
    public List<string> Hashes { get; set; } = [];
}