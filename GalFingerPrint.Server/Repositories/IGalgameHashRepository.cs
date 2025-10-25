namespace GalFingerPrint.Server.Repositories;

public interface IGalgameHashRepository
{
    /// 为指定游戏-哈希关系增加或减少计数（delta 可为正负）。
    Task AddDeltaAsync(int galgameId, int hashId, int delta, CancellationToken ct = default);
}

