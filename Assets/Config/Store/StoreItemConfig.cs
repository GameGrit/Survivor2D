using UnityEngine;

/// <summary>
/// 商店商品数据配置（ScriptableObject纯数据层）
/// 职责：只存储商品静态数据，不持有任何场景组件引用
/// </summary>
[CreateAssetMenu(fileName = "StoreItem_", menuName = "Store/商品配置")]
public class StoreItemConfig : ScriptableObject
{
    [Header("背包关联")]
    [Tooltip("购买后生成到背包里的物品配置")]
    public BagItemConfig correspondingBagItem;
    [Header("基础信息")]
    [Tooltip("商品唯一ID，用于存档和查找")]
    public string itemId;

    [Tooltip("商品显示名称")]
    public string itemName;

    [Tooltip("商品描述")]
    [TextArea(2, 4)]
    public string description;

    [Header("显示资源")]
    [Tooltip("商品图标（注意：是Sprite，不是SpriteRenderer）")]
    public Sprite icon;

    [Header("价格")]
    [Tooltip("商品价格")]
    public int price;

    [Tooltip("货币类型（0=金币,1=钻石）")]
    public int currencyType;
}
