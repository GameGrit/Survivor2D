using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PoolManager : BaseMonoSingleton<PoolManager>
{
    private readonly Dictionary<GameObject, ObjectPoolBase> _poolDict = new Dictionary<GameObject, ObjectPoolBase>();

    protected override void Awake()
    {
        base.Awake();
        // 【关键】监听场景加载，切场景时清空所有池
        // 原因：PoolManager 是 DontDestroyOnLoad 单例，切场景后旧场景的 parent（如 bulletRootTransform）
        // 已被销毁，但 _poolDict 里还存着旧的 ObjectPoolBase，其 _parent 指向已销毁对象。
        // 第二局复用旧池时 SetParent 访问已销毁引用 → MissingReferenceException（即用户说的"爆死"）
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>场景加载完成回调：清空所有对象池，避免旧场景引用残留</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearAll();
        Debug.Log($"[PoolManager] 场景「{scene.name}」加载完成，已清空所有对象池");
    }

    public GameObject Get(GameObject prefab, Transform parent = null, int initCount = 10)
    {
        if (prefab == null) return null;

        if (!_poolDict.ContainsKey(prefab))
        {
            Transform poolParent = parent ?? transform;
            _poolDict[prefab] = new ObjectPoolBase(prefab, poolParent, initCount);
        }

        ObjectPoolBase pool = _poolDict[prefab];
        GameObject go = pool.Get();
        return go;
    }

    public void Recycle(GameObject prefab, GameObject go)
    {
        if (prefab == null || go == null) return;

        if (_poolDict.TryGetValue(prefab, out ObjectPoolBase pool))
        {
            pool.Recycle(go);
        }
        else
        {
            Destroy(go);
        }
    }

    public void ClearAll()
    {
        foreach (var pool in _poolDict.Values)
        {
            pool.Clear();
        }
        _poolDict.Clear();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ClearAll();
    }
}
