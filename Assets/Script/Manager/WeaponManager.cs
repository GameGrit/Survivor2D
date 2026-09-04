using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器管理器 —— 实例化预制体方案
///
/// 【设计思路】
///   - 切换武器时销毁旧武器预制体，实例化新武器预制体到挂载点
///   - 武器预制体上已配好 Transform、SpriteRenderer、WeaponDir、发射脚本
///   - 通过 localPositionOffset 微调位置，解决不同武器 pivot 不同导致的位移
///
/// 【跨场景武器选择】
///   - 静态字段 SelectedWeaponId 记录玩家在开始界面选中的武器
///   - 每个场景的 WeaponManager 在 Start 时从这个静态字段读取并初始化
///
/// 【执行顺序】
///   - DefaultExecutionOrder(-100) 确保 WeaponManager 比 PlayerAutoWeapon 先初始化
/// </summary>
[DefaultExecutionOrder(-100)]
public class WeaponManager : BaseMonoSingleton<WeaponManager>
{
    [Header("Addressables 路径名（在 Groups 窗口里设置的路径名）")]
    public string weaponListAddress = "武器列表";
    private WeaponListConfig _configList;
    public WeaponListConfig configList => _configList;
    [Header("玩家身上武器挂载点（拖 Player 下的空物体）")]
    public Transform weaponHoldPoint;

    // ============================================================
    //  跨场景静态武器选择
    // ============================================================
    /// <summary>玩家当前选中的武器ID（开始界面设置，游戏场景读取）</summary>
    public static int SelectedWeaponId = 0;

    // ============================================================
    //  当前武器状态（对外只读）
    // ============================================================
    /// <summary>当前生效的武器配置</summary>
    public WeaponConfig CurrentWeaponConfig { get; private set; }

    /// <summary>当前实例化的武器物体（预制体实例，自带 WeaponDir + 发射脚本）</summary>
    public GameObject CurrentWeaponObj { get; private set; }

    /// <summary>当前武器的枪口 Transform（从预制体的 FirePoint 子节点取）</summary>
    public Transform CurrentFirePoint { get; private set; }

    /// <summary>
    /// 重写Awake：不调用base.Awake，不做DontDestroyOnLoad
    /// 每个场景独立的WeaponManager，通过静态SelectedWeaponId跨场景传递选择
    /// </summary>
    protected override void Awake()
    {
        // 不调用 base.Awake()，避免 DontDestroyOnLoad
    }

    private void Start()
    {
        // 从 Addressables 加载武器配置表
        _configList = AddressablesManager.Instance.LoadAssetSync<WeaponListConfig>(weaponListAddress);
        if (_configList == null)
        {
            Debug.LogError("[WeaponManager] 武器配置表加载失败！检查 Addressable 路径名是否是 weapon_list");
            return;
        }

        Debug.Log($"[WeaponManager] Start() 执行，当前 SelectedWeaponId={SelectedWeaponId}，配置表已加载，weaponHoldPoint={(weaponHoldPoint == null ? "空" : weaponHoldPoint.name)}");
        InitWeapon();
    }

    /// <summary>
    /// 初始化武器：从静态选中ID读取，切换到对应武器
    /// </summary>
    void InitWeapon()
    {
        if (configList == null)
        {
            Debug.LogError("[WeaponManager] WeaponListConfig 没有赋值！无法初始化武器！");
            return;
        }
        Debug.Log($"[WeaponManager] InitWeapon 准备切换到 id={SelectedWeaponId}");
        SwitchWeapon(SelectedWeaponId);
    }

    /// <summary>
    /// 切换武器：销毁旧武器，实例化新武器，更新当前武器状态
    /// </summary>
    /// <param name="weaponId">武器配置表中的 id</param>
    public void SwitchWeapon(int weaponId)
    {
        if (configList == null) return;

        WeaponConfig newCfg = configList.GetWeaponById(weaponId);
        if (newCfg == null)
        {
            Debug.LogError($"WeaponManager: 找不到 id={weaponId} 的武器配置！回退到id=0");
            newCfg = configList.GetWeaponById(0);
            if (newCfg == null) return;
            weaponId = 0;
        }

        CurrentWeaponConfig = newCfg;
        // 同步更新静态选中ID（确保切场景后保持一致）
        SelectedWeaponId = weaponId;

        // 销毁旧武器
        if (CurrentWeaponObj != null)
        {
            Destroy(CurrentWeaponObj);
            CurrentWeaponObj = null;
        }

        // 实例化新武器到挂载点
        if (weaponHoldPoint != null && newCfg.weaponPrefab != null)
        {
            CurrentWeaponObj = Instantiate(newCfg.weaponPrefab, weaponHoldPoint);
            // 应用位置偏移（解决不同武器 pivot 不同导致的位移）
            CurrentWeaponObj.transform.localPosition = newCfg.localPositionOffset;
            CurrentWeaponObj.transform.localRotation = Quaternion.identity;

            // 从武器预制体里找枪口子节点（约定命名为 "FirePoint"）
            CurrentFirePoint = CurrentWeaponObj.transform.Find("FirePoint");

            Debug.Log($"[WeaponManager] 武器实例化成功：{newCfg.weaponName}，位置={CurrentWeaponObj.transform.localPosition}，FirePoint={(CurrentFirePoint == null ? "未找到" : "已找到")}");
        }
        else
        {
            if (weaponHoldPoint == null)
                Debug.LogError("[WeaponManager] weaponHoldPoint 为空！无法实例化武器！");
            if (newCfg.weaponPrefab == null)
                Debug.LogError($"[WeaponManager] 武器「{newCfg.weaponName}」的 weaponPrefab 为空！");
            CurrentFirePoint = null;
        }

        // 主动通知 PlayerAutoWeapon 立即刷新发射策略
        Player.PlayerAutoWeapon autoWeapon = FindObjectOfType<Player.PlayerAutoWeapon>();
        if (autoWeapon != null)
        {
            autoWeapon.RefreshWeaponParams();
        }

        Debug.Log($"[WeaponManager] 已切换武器：{newCfg.weaponName} (id={weaponId})，模式=实例化预制体");
    }

    /// <summary>
    /// 清空当前武器（切场景或角色死亡时调用）
    /// </summary>
    public void ClearWeapon()
    {
        if (CurrentWeaponObj != null)
        {
            Destroy(CurrentWeaponObj);
            CurrentWeaponObj = null;
        }
        CurrentWeaponConfig = null;
        CurrentFirePoint = null;
    }
}
