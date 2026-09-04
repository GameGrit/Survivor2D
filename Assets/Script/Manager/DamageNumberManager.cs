using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 伤害数字管理器，单例，统一管理所有伤害数字的对象池
/// </summary>
public class DamageNumberManager : BaseMonoSingleton<DamageNumberManager>
{
    [Header("伤害数字预制体")]
    public GameObject damageNumberPrefab;

    [Header("对象池配置")]
    public int initPoolCount = 20;
    public int maxPoolCount = 50;

    [Header("画布引用（伤害数字挂在这下面）")]
    public Transform canvasTransform;

    // 对象池队列
    private Queue<DamageNumber> _poolQueue = new Queue<DamageNumber>();
    private int _currentAliveCount;

    protected override void Awake()
    {
        base.Awake();

        // 初始化对象池
        if (damageNumberPrefab != null && canvasTransform != null)
        {
            InitPool();
            Debug.Log($"[DamageNumberManager] 对象池初始化完成，初始数量={initPoolCount}");
        }
        else
        {
            Debug.LogError($"[DamageNumberManager] ❌ 配置不完整！请在场景中手动放置 DamageNumberManager 物体，" +
                           $"并拖入 damageNumberPrefab 和 canvasTransform！\n" +
                           $"当前物体名：{gameObject.name}（如果是自动创建的空物体，说明场景里没放）");
        }
    }

    /// <summary>
    /// 重置管理器 —— 切场景/继续存档后必须调用
    /// 【为什么需要】DontDestroyOnLoad单例切场景后，canvasTransform 还指向旧画布（已销毁），
    /// 对象池里的 DamageNumber 也随旧画布销毁了，导致 ShowDamage 时创建不了新数字
    /// </summary>
    public void ResetManager()
    {
        // 1. 清空旧对象池（里面的对象已随旧画布销毁）
        while (_poolQueue.Count > 0)
        {
            DamageNumber old = _poolQueue.Dequeue();
            if (old != null && old.gameObject != null)
                Destroy(old.gameObject);
        }
        _currentAliveCount = 0;

        // 2. 重新查找当前场景的画布（旧画布已销毁）
        if (canvasTransform == null || canvasTransform.gameObject == null || !canvasTransform.gameObject.activeInHierarchy)
        {
            Canvas[] allCanvas = FindObjectsOfType<Canvas>();
            Canvas target = null;

            // 优先找名字含 HUD/Game/Main 的画布
            foreach (Canvas c in allCanvas)
            {
                string name = c.gameObject.name.ToLower();
                if (name.Contains("hud") || name.Contains("game") || name.Contains("main") || name.Contains("ui"))
                {
                    target = c;
                    break;
                }
            }
            // 找不到就用第一个
            if (target == null && allCanvas.Length > 0)
                target = allCanvas[0];

            if (target != null)
            {
                canvasTransform = target.transform;
                Debug.Log($"[DamageNumberManager] ResetManager 重新找到画布：{target.gameObject.name}");
            }
            else
            {
                Debug.LogError("[DamageNumberManager] ResetManager 找不到任何画布！伤害数字将无法显示");
                return;
            }
        }

        // 3. 重新初始化对象池
        if (damageNumberPrefab != null && canvasTransform != null)
        {
            InitPool();
            Debug.Log($"[DamageNumberManager] ResetManager 对象池重建完成，初始数量={initPoolCount}");
        }
        else
        {
            Debug.LogError("[DamageNumberManager] ResetManager 配置仍不完整，prefab或canvas为空");
        }
    }

    /// <summary>初始化对象池</summary>
    void InitPool()
    {
        for (int i = 0; i < initPoolCount; i++)
        {
            DamageNumber dn = CreateNewOne();
            dn.gameObject.SetActive(false);
            _poolQueue.Enqueue(dn);
        }
    }

    /// <summary>创建一个新的伤害数字</summary>
    DamageNumber CreateNewOne()
    {
        if (damageNumberPrefab == null || canvasTransform == null)
        {
            Debug.LogError("[DamageNumberManager] prefab或canvas为null，无法创建伤害数字");
            return null;
        }

        GameObject go = Instantiate(damageNumberPrefab, canvasTransform);
        DamageNumber dn = go.GetComponent<DamageNumber>();
        if (dn == null)
            dn = go.AddComponent<DamageNumber>();

        dn.OnRecycleCallback += OnDamageNumberRecycle;
        return dn;
    }

    /// <summary>
    /// 显示伤害数字
    /// </summary>
    /// <param name="worldPos">世界坐标位置</param>
    /// <param name="damage">伤害数值</param>
    /// <param name="isCritical">是否暴击</param>
    /// <param name="isPlayerHurt">是否是玩家受伤（红色）</param>
    public void ShowDamage(Vector3 worldPos, float damage, bool isCritical = false, bool isPlayerHurt = false)
    {
        // 到达上限就不显示了，防止卡顿
        if (_currentAliveCount >= maxPoolCount)
        {
            Debug.LogWarning($"[DamageNumberManager] 达到最大显示数量 {maxPoolCount}，跳过");
            return;
        }

        // 从池里取，跳过已销毁的对象（切场景后池子里的对象可能被销毁）
        DamageNumber dn = null;
        while (_poolQueue.Count > 0)
        {
            dn = _poolQueue.Dequeue();
            if (dn != null && dn.gameObject != null) break; // 找到有效对象
            // dn已销毁，继续取下一个
        }

        if (dn != null)
        {
            dn.gameObject.SetActive(true);
            // 塞到 Canvas 子节点最底层，确保 UI 面板（暂停/结算/升级）能盖在伤害数字上面
            dn.transform.SetAsFirstSibling();
        }
        else
        {
            // 队列空了或者全是已销毁对象，重新创建
            // 切场景后 canvasTransform / damageNumberPrefab 可能已销毁，检查后再创建
            if (damageNumberPrefab == null || canvasTransform == null)
            {
                Debug.LogWarning("[DamageNumberManager] prefab或canvas已销毁，跳过显示伤害数字");
                return;
            }
            dn = CreateNewOne();
            Debug.Log($"[DamageNumberManager] 对象池不够或已销毁，新建一个，当前存活={_currentAliveCount}");
        }

        if (dn == null) return;

        _currentAliveCount++;

        // 世界坐标转屏幕坐标（UI用）
        if (Camera.main == null)
        {
            Debug.LogError("[DamageNumberManager] ❌ Camera.main 是 null！请把相机 Tag 设为 MainCamera");
            return;
        }
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        dn.transform.position = screenPos;

        // 设置数值
        dn.SetDamage(damage, isCritical, isPlayerHurt);
    }

    /// <summary>伤害数字回池回调</summary>
    void OnDamageNumberRecycle(DamageNumber dn)
    {
        // 已销毁的对象不回收
        if (dn == null || dn.gameObject == null)
        {
            _currentAliveCount--;
            return;
        }

        dn.gameObject.SetActive(false);
        _poolQueue.Enqueue(dn);
        _currentAliveCount--;
    }

    /// <summary>清空所有伤害数字</summary>
    public void ClearAll()
    {
        while (_poolQueue.Count > 0)
        {
            DamageNumber dn = _poolQueue.Dequeue();
            if (dn != null && dn.gameObject != null)
                Destroy(dn.gameObject);
        }
        _currentAliveCount = 0;
    }

    /// <summary>隐藏所有正在显示的伤害数字（游戏结束/暂停时调用，避免飘字盖住面板）</summary>
    public void HideAllActive()
    {
        if (canvasTransform == null) return;
        DamageNumber[] all = canvasTransform.GetComponentsInChildren<DamageNumber>(true);
        foreach (DamageNumber dn in all)
        {
            if (dn != null && dn.gameObject != null && dn.gameObject.activeSelf)
            {
                dn.gameObject.SetActive(false);
                _poolQueue.Enqueue(dn);
                _currentAliveCount--;
            }
        }
    }
}
