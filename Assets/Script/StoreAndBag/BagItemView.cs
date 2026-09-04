using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 背包物品卡片视图（显示层）
/// 职责：根据配置数据刷新UI显示，转发点击事件，不做业务逻辑
///
/// 【装备标识】
///   isReady GameObject 用于显示"已装备"状态
///   由 BagAndStoreManager.SetEquippedWeapon() 统一控制
/// </summary>
public class BagItemView : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Image iconImage;
    [Tooltip("已装备标识（勾选框/边框等），装备时显示")]
    public GameObject isReady;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("点击按钮（不填则自动GetComponent）")]
    [SerializeField] private Button clickButton;

    /// <summary>当前背包物品配置</summary>
    public BagItemConfig currentConfig;

    /// <summary>点击事件：参数为该物品的配置</summary>
    public event Action<BagItemConfig> OnItemClicked;

    private void Awake()
    {
        // 自动获取Button组件
        if (clickButton == null)
            clickButton = GetComponent<Button>();
        if (clickButton == null)
            clickButton = GetComponentInChildren<Button>();

        if (clickButton != null)
        {
            clickButton.onClick.AddListener(OnClick);
        }
        else
        {
            Debug.LogWarning($"[BagItemView] 物体 {gameObject.name} 上没有Button组件，点击事件无法触发！");
        }

        // 【兜底】如果 isReady 没在 Inspector 赋值，自动查找子物体
        // 动态生成的卡片用的是 Addressables 预制体，可能 isReady 字段没赋值
        if (isReady == null)
        {
            // 按名字查找：isReady / IsReady / 已准备 / ReadyMark / EquippedMark
            Transform found = transform.Find("isReady")
                           ?? transform.Find("IsReady")
                           ?? transform.Find("已准备")
                           ?? transform.Find("ReadyMark")
                           ?? transform.Find("EquippedMark");
            if (found != null)
            {
                isReady = found.gameObject;
                Debug.Log($"[BagItemView] {gameObject.name} 自动找到 isReady 物体：{isReady.name}");
            }
            else
            {
                Debug.LogWarning($"[BagItemView] {gameObject.name} 找不到 isReady 物体！请把'已准备'标识拖到 isReady 字段，或把子物体命名为 isReady");
            }
        }

        // 如果在Inspector中预先拖了currentConfig（编辑器预先摆好的背包物品），自动刷新UI
        // 这样不需要通过代码调用SetData，直接在编辑器里配好就能用
        if (currentConfig != null)
        {
            RefreshUI();
        }

        // 【关键修复】默认隐藏装备标识，必须放在 Awake 末尾，不能放在 Start！
        // 原因：动态 Instantiate 后，BagAndStoreManager.RefreshEquipMarks() 会在同一帧
        // 调用 SetEquipped(true) 把 isReady 打开；但 Start() 在下一帧才执行，
        // 会把 isReady 重新设为 false，导致装备标识丢失（从游戏场景退回开始界面时必现）。
        // Awake 在 Instantiate 时同步执行，早于 SetEquipped，所以放在这里安全。
        if (isReady != null)
            isReady.SetActive(false);
    }

    private void Start()
    {
        // Start 中不再做默认隐藏，避免覆盖 RefreshEquipMarks 设置的装备标识
    }

    /// <summary>
    /// 点击回调：转发点击事件给外部
    /// </summary>
    private void OnClick()
    {
        if (currentConfig == null)
        {
            Debug.LogError($"[BagItemView] 物体「{gameObject.name}」的 currentConfig 为空！\n" +
                $"如果是编辑器中预先摆好的背包物品，请在Inspector中把对应的 BagItemConfig 资产拖到 currentConfig 字段。\n" +
                $"如果是动态生成的，确保调用了 SetData(config)。");
            return;
        }
        Debug.Log($"[BagItemView] 点击背包物品：{currentConfig.bagItemName} (weaponId={currentConfig.weaponId})");
        OnItemClicked?.Invoke(currentConfig);
    }

    /// <summary>
    /// 注入背包物品数据并刷新UI
    /// </summary>
    public void SetData(BagItemConfig config)
    {
        currentConfig = config;
        RefreshUI();
    }

    /// <summary>
    /// 设置装备标识显隐
    /// </summary>
    public void SetEquipped(bool equipped)
    {
        if (isReady != null)
        {
            isReady.SetActive(equipped);
            Debug.Log($"[BagItemView] {gameObject.name} ({currentConfig?.bagItemName}) SetEquipped={equipped}，isReady={(isReady != null ? isReady.name : "NULL")}");
        }
        else
        {
            Debug.LogError($"[BagItemView] {gameObject.name} SetEquipped 失败：isReady 为 null！请把'已准备'标识拖到 isReady 字段");
        }
    }

    /// <summary>
    /// 判断这个背包物品是否是武器（weaponId >= 0）
    /// </summary>
    public bool IsWeapon()
    {
        return currentConfig != null && currentConfig.weaponId >= 0;
    }

    private void RefreshUI()
    {
        if (currentConfig == null)
        {
            Debug.LogError("[BagItemView] 背包物品配置为空");
            return;
        }

        if (iconImage != null)
            iconImage.sprite = currentConfig.spriteItem;

        if (nameText != null)
            nameText.text = currentConfig.bagItemName;
    }

    public BagItemConfig GetConfig() => currentConfig;
}
