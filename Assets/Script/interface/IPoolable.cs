public interface IPoolable
{
    /// <summary>从池中拿出来的时候调用，重置状态</summary>
    void OnSpawn();
    /// <summary>放回池的时候调用，清理状态</summary>
    void OnDespawn();
}
