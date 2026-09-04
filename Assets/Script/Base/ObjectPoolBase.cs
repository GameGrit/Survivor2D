using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolBase
{
    private readonly Queue<GameObject> _queue = new Queue<GameObject>();
    private readonly GameObject _prefab;
    private readonly Transform _parent;

    // 【关键】追踪所有创建过的实例（包括场上活跃的和池子里等待的）
    // 原因：Clear() 时必须销毁所有实例，否则切场景后场上活跃的怪物/子弹不会被销毁，
    // 它们的 Update/FixedUpdate 还在跑，而 Player 已被场景切换销毁，
    // 导致每帧报"找不到 Tag=Player"、距离=float.MaxValue 刷屏
    private readonly HashSet<GameObject> _allInstances = new HashSet<GameObject>();

    public ObjectPoolBase(GameObject prefab, Transform parent, int initCount)
    {
        _prefab = prefab;
        _parent = parent;

        for (int i = 0; i < initCount; i++)
        {
            GameObject go = Object.Instantiate(_prefab, _parent);
            go.SetActive(false);
            _queue.Enqueue(go);
            _allInstances.Add(go); // 记录到全实例追踪表
        }
    }

    public GameObject Get()
    {
        GameObject go = null;

        // 从队列里取，跳过已销毁的对象
        // 【为什么需要】PoolManager是DontDestroyOnLoad单例，切场景后池子里的GameObject被销毁，
        // 但Queue里还存着引用，Dequeue出来访问go.transform就报MissingReferenceException
        while (_queue.Count > 0)
        {
            go = _queue.Dequeue();
            if (go != null) break; // 找到有效对象，跳出
            // go是null（已销毁），继续取下一个
        }

        // 队列空了或者全是已销毁对象，重新创建
        if (go == null)
        {
            // _parent也可能已销毁（传入的是场景对象），用null让Instantiate创建在根节点
            Transform parent = _parent != null ? _parent : null;
            go = Object.Instantiate(_prefab, parent);
            _allInstances.Add(go); // 动态创建的也记录到全实例追踪表
        }

        // parent可能已销毁，检查后再设置
        if (_parent != null)
        {
            go.transform.SetParent(_parent);
        }
        go.SetActive(true);

        // 取出后执行初始化
        if (go.TryGetComponent(out IPoolable poolable))
        {
            poolable.OnSpawn();
        }
        return go;
    }

    public void Recycle(GameObject go)
    {
        // 已销毁的对象不回收
        if (go == null) return;

        if (go.TryGetComponent(out IPoolable poolable))
        {
            poolable.OnDespawn();
        }
        go.SetActive(false);
        _queue.Enqueue(go);
    }

    public void Clear()
    {
        // 【关键】销毁所有创建过的实例，包括场上活跃的（不在_queue里）
        // 旧实现只清_queue，导致切场景后活跃怪物没被销毁，FSM还在跑，每帧刷屏报错
        foreach (GameObject go in _allInstances)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }
        _allInstances.Clear();
        _queue.Clear();
    }
}
