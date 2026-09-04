using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 商店商品卡片视图（显示层）
/// 职责：根据配置数据刷新UI显示，不做任何业务逻辑
/// </summary>
public class StoreItemView : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button buyButton;
    // 定义委托：当本物品被点击，把自己的配置传出去
    public System.Action<StoreItemConfig> OnItemClicked;

    public StoreItemConfig currentConfig;
    private void Start()
    {

    }
    /// <summary>
    /// 注入商品数据并刷新UI（企业级标准：通过方法注入，不暴露公共字段）
    /// </summary>
    public void SetData(StoreItemConfig config)
    {
        currentConfig = config;
        RefreshUI();

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnItemclick);
        }
    }

    /// <summary>
    /// 刷新UI显示
    /// </summary>
    private void RefreshUI()
    {
        if (currentConfig == null)
        {
            Debug.LogError("[StoreItemView] 商品配置为空，无法刷新UI");
            return;
        }


        // 图标
        if (iconImage != null)
            iconImage.sprite = currentConfig.icon;

        // 名称
        if (nameText != null)
            nameText.text = currentConfig.itemName;

        // 价格
        if (priceText != null)
            priceText.text = currentConfig.price.ToString();
    }

    /// <summary>
    /// 获取当前商品配置（供外部购买逻辑使用）
    /// </summary>
    public StoreItemConfig GetConfig() => currentConfig;
    public void OnItemclick()
    {
        // 把自己的配置抛出去
        OnItemClicked?.Invoke(currentConfig);   
    }
}
