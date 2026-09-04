using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BagAndStoreManager : BaseMonoSingleton<BagAndStoreManager>
{
    [Header("Addressables 路径名/Label")]
    public string storeItemsLabel = "store_item";
    public string itemPrefabAddress = "store_item_view";
    public string bagItemPrefabAddress = "bag_item_view";

    private List<StoreItemConfig> _itemConfigs;
    public List<StoreItemConfig> itemConfigs => _itemConfigs;

    private StoreItemView _itemPrefab;
    public StoreItemView itemPrefab => _itemPrefab;

    private BagItemView _bagItemPrefab;
    public BagItemView bagItemPrefab => _bagItemPrefab;

    [Tooltip("商品列表父容器（GridLayoutGroup所在物体）")]
    [SerializeField] private Transform contentParent;
    public StoreAndBagPanel storeAndBagPanel;
    private StoreItemConfig _selectedItemConfig;
    public TipsPanel _tipsPanel;
    [Tooltip("背包列表父节点（GridLayoutGroup所在物体）")]
    [SerializeField] private Transform bagContentParent;

    // 运行时记录当前商店里生成的卡片，方便按配置找到并销毁
    private List<StoreItemView> spawnedStoreItems = new List<StoreItemView>();

    // 已购买商品ID记录：退出商店再进来时，已购买的不再生成
    private HashSet<string> _purchasedItemIds = new HashSet<string>();

    // 已购买的背包物品配置记录：重新打开商城时，根据此列表重新生成背包子物体
    private List<BagItemConfig> _purchasedBagItems = new List<BagItemConfig>();

    // 运行时记录生成的背包物品卡片，方便刷新装备标识
    private List<BagItemView> _spawnedBagItems = new List<BagItemView>();

    // 当前装备的武器ID（-1表示未装备）
    private int _equippedWeaponId = -1;

    protected override void Awake()
    {
        Debug.Log($"[BagAndStoreManager] Awake 执行，物体={gameObject.name}, activeSelf={gameObject.activeSelf}");
        base.Awake();

        // 从 Addressables 批量加载商店商品配置
        _itemConfigs = AddressablesManager.Instance.LoadAssetsByLabelSync<StoreItemConfig>(storeItemsLabel);
        Debug.Log($"[BagAndStoreManager] 从 Addressables 加载了 {_itemConfigs.Count} 个商店商品");

        // 加载 UI 预制体
        GameObject itemGo = AddressablesManager.Instance.LoadAssetSync<GameObject>(itemPrefabAddress);
        if (itemGo != null) _itemPrefab = itemGo.GetComponent<StoreItemView>();
        else Debug.LogError("[BagAndStoreManager] 商店卡片预制体加载失败，检查路径名 store_item_view");

        GameObject bagGo = AddressablesManager.Instance.LoadAssetSync<GameObject>(bagItemPrefabAddress);
        if (bagGo != null) _bagItemPrefab = bagGo.GetComponent<BagItemView>();
        else Debug.LogError("[BagAndStoreManager] 背包卡片预制体加载失败，检查路径名 bag_item_view");

        LoadFromSave();
        Init();
    }
    public void Start()
    {
        if (storeAndBagPanel == null)
        {
            storeAndBagPanel = FindObjectOfType<StoreAndBagPanel>();
        }

        // 订阅 TipsPanel 的确定按钮事件（配合脚本二的改动）
        if (_tipsPanel != null)
        {
            _tipsPanel.OnConfirm += TryBuySelectedItem;
        }
        else
        {
            Debug.LogError("[BagAndStoreManager] _tipsPanel 未在 Inspector 赋值");
        }

        // 扫描背包容器下已有的BagItemView（编辑器中预先摆好的武器图标）
        ScanExistingBagItems();

        // 从跨场景静态字段同步当前装备的武器，并刷新装备标识
        // 关键：只有已装备(>=0)时才同步，未装备(-1)时保持默认手枪(SelectedWeaponId=0)
        if (_equippedWeaponId >= 0)
        {
            WeaponManager.SelectedWeaponId = _equippedWeaponId;
        }
        else
        {
            WeaponManager.SelectedWeaponId = 0; // 未装备时默认手枪
        }


        RefreshEquipMarks();

        // 【关键修复】不能用 GameManager.Instance，它在找不到时会自动创建空物体并 DontDestroyOnLoad，
        // 导致切到游戏场景后 GameRoot 上真正的 GameManager 因单例重复而销毁整个 GameRoot。
        // 用 FindObjectOfType 只查找、不创建。
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            gm.PauseGame();
        }
    }

    /// <summary>
    /// 生成商店商品列表
    /// </summary>

    private void Init()
    {
        Debug.Log($"[Init] 开始生成商品，itemConfigs 数量 = {(itemConfigs == null ? 0 : itemConfigs.Count)}");

        if (contentParent == null)
        {
            Debug.LogError("[Init] contentParent 未赋值，请在 Inspector 拖入商店列表的 Grid 父容器");
            return;
        }
        if (itemPrefab == null)
        {
            Debug.LogError("[Init] itemPrefab 未赋值，请在 Inspector 拖入 StoreItemView 预制体");
            return;
        }
        if (itemConfigs == null || itemConfigs.Count == 0)
        {
            Debug.LogError("[Init] itemConfigs 列表为空！请在 BagAndStoreManager 的 Inspector 里把商品配置资产拖入 itemConfigs 列表");
            return;
        }

        foreach (Transform child in contentParent)
            Destroy(child.gameObject);
        // 关键：清空运行时记录，否则重复 Init 后列表里残留已销毁的旧引用，
        // RemoveStoreItem 的 Find 会先匹配到旧引用导致实际显示的卡片删不掉
        spawnedStoreItems.Clear();

        int spawnCount = 0;
        foreach (var config in itemConfigs)
        {
            if (config == null)
            {
                Debug.LogWarning("[Init] itemConfigs 中有一个空元素，跳过");
                continue;
            }
            // 已购买的商品不再生成
            if (!string.IsNullOrEmpty(config.itemId) && _purchasedItemIds.Contains(config.itemId))
            {
                Debug.Log($"[Init] 商品 {config.itemName} 已购买，跳过生成");
                continue;
            }
            StoreItemView item = Instantiate(itemPrefab, contentParent);
            item.SetData(config);
            item.OnItemClicked += OnSelectStoreItem;
            spawnedStoreItems.Add(item);
            spawnCount++;
            Debug.Log($"[Init] 生成商品：{config.itemName}");
        }
        Debug.Log($"[Init] 商品生成完毕，共生成 {spawnCount} 个，contentParent 子物体数量 = {contentParent.childCount}");
    }


    /// <summary>
    /// 当点击商品卡片，得到选中物品配置
    /// </summary>
    void OnSelectStoreItem(StoreItemConfig selectConfig)
    {
        _selectedItemConfig = selectConfig;
        Debug.Log($"选中物品：{selectConfig.itemName}，价格：{selectConfig.price}");

        if (_tipsPanel != null)
        {
            _tipsPanel.Show("是否需要购买");
        }
    }

    /// <summary>
    /// 是否可以购买
    /// </summary>

    public void TryBuySelectedItem()
    {
        if (_selectedItemConfig == null)
        {
            Debug.LogError("[BagAndStoreManager] 没有选中商品");
            return;
        }

        int price = _selectedItemConfig.price;
        TipsPanel _tipsPanel = FindObjectOfType<TipsPanel>();
        // 走 CoinManager 扣金币，自动发事件，商店和游戏界面同时刷新
        if (CoinManager.Instance.SpendCoin(price))
        {
            // 记录已购买，下次打开商店不再生成
            if (!string.IsNullOrEmpty(_selectedItemConfig.itemId))
                _purchasedItemIds.Add(_selectedItemConfig.itemId);

            // 加入背包
            AddToBag(_selectedItemConfig);

            // 从商店列表移除
            RemoveStoreItem(_selectedItemConfig);

            Debug.Log($"购买成功：{_selectedItemConfig.itemName}，剩余金币：{CoinManager.Instance.CurrentCoin}");
            SaveToSave();

            if (_tipsPanel != null)
                _tipsPanel.Show("购买成功");
        }
        else
        {
            Debug.LogWarning($"金币不足！需要{price}，当前{CoinManager.Instance.CurrentCoin}");
            if (_tipsPanel != null)
                _tipsPanel.Show("金币不足");
        }
    }
    public void Reinit()
    {
        // 重新找当前场景的UI（旧实例的引用都失效了）
        storeAndBagPanel = FindObjectOfType<StoreAndBagPanel>(true);
        _tipsPanel = FindObjectOfType<TipsPanel>(true);

        Debug.Log($"[Reinit] storeAndBagPanel={(storeAndBagPanel == null ? "NULL" : storeAndBagPanel.name)}, 旧bagContentParent={(bagContentParent == null ? "NULL(已失效)" : bagContentParent.name)}");

        // 从商店面板拿父容器引用
        if (storeAndBagPanel != null)
        {
            if (storeAndBagPanel.storeContentParent != null)
                contentParent = storeAndBagPanel.storeContentParent;
            if (storeAndBagPanel.bagContentParent != null)
                bagContentParent = storeAndBagPanel.bagContentParent;
        }

        Debug.Log($"[Reinit] 更新后 bagContentParent={(bagContentParent == null ? "NULL" : bagContentParent.name)}, _purchasedBagItems={(_purchasedBagItems == null ? 0 : _purchasedBagItems.Count)}, _equippedWeaponId={_equippedWeaponId}");

        // 重新绑定确认事件
        if (_tipsPanel != null)
        {
            _tipsPanel.OnConfirm -= TryBuySelectedItem;
            _tipsPanel.OnConfirm += TryBuySelectedItem;
        }

        // 重新生成商品列表
        Init();

        // 重新生成背包里已购买的物品
        RefreshBag();
    }

    /// <summary>
    /// 重新打开商城时调用：清理旧引用，扫描预先摆好的物品，
    /// 并根据 _purchasedBagItems 记录重新生成已购买的背包子物体
    /// </summary>
    private void RefreshBag()
    {
        if (bagContentParent == null) return;

        // 【关键修复】清空整个列表，而不是只 RemoveAll(null)
        // 切场景后 Unity 的 ==null 重载有延迟，旧引用可能没被清理掉，
        // 导致 exists 检查匹配到旧残留引用，新物品不生成，装备标识丢失
        _spawnedBagItems.Clear();

        // 扫描当前面板中编辑器预先摆好的背包物品
        ScanExistingBagItems();

        // 根据已购买记录，把缺失的物品重新生成到背包里
        foreach (var bagConfig in _purchasedBagItems)
        {
            if (bagConfig == null) continue;

            // 避免重复生成：检查当前背包里是否已经有同配置的物品
            bool exists = _spawnedBagItems.Exists(item => item != null && item.GetConfig() == bagConfig);
            if (exists) continue;

            if (bagItemPrefab == null)
            {
                Debug.LogError("[RefreshBag] bagItemPrefab 为空，无法生成背包物品");
                continue;
            }

            BagItemView bagItem = Instantiate(bagItemPrefab, bagContentParent);
            bagItem.SetData(bagConfig);
            bagItem.OnItemClicked -= OnBagItemClicked;
            bagItem.OnItemClicked += OnBagItemClicked;

            _spawnedBagItems.Add(bagItem);
            Debug.Log($"[RefreshBag] 重新生成背包物品：{bagConfig.bagItemName}");
        }

        // 刷新装备标识
        RefreshEquipMarks();
    }



    /// <summary>
    /// 购买成功后，把对应物品生成到背包里
    /// </summary>
    private void AddToBag(StoreItemConfig storeConfig)
    {
        Debug.Log($"[AddToBag] 开始添加，商品={storeConfig.itemName}");

        if (storeConfig.correspondingBagItem == null)
        {
            Debug.LogError($"[AddToBag] 商品 {storeConfig.itemName} 的 correspondingBagItem 为空，请在该商品的 ScriptableObject 里拖入背包配置");
            return;
        }
        Debug.Log($"[AddToBag] correspondingBagItem = {storeConfig.correspondingBagItem.bagItemName}");

        if (bagItemPrefab == null)
        {
            Debug.LogError("[AddToBag] bagItemPrefab 为空，请在 BagAndStoreManager 的 Inspector 里拖入背包物品预制体");
            return;
        }
        if (bagContentParent == null)
        {
            Debug.LogError("[AddToBag] bagContentParent 为空，请在 BagAndStoreManager 的 Inspector 里拖入背包列表的父容器");
            return;
        }
        Debug.Log($"[AddToBag] bagContentParent = {bagContentParent.name}, activeSelf={bagContentParent.gameObject.activeSelf}");

        BagItemView bagItem = Instantiate(bagItemPrefab, bagContentParent);
        bagItem.SetData(storeConfig.correspondingBagItem);
        // 注册点击事件：点击背包物品时处理装备
        bagItem.OnItemClicked += OnBagItemClicked;
        _spawnedBagItems.Add(bagItem);

        // 记录已购买的背包物品配置，重新打开商城时据此重新生成
        if (!_purchasedBagItems.Contains(storeConfig.correspondingBagItem))
            _purchasedBagItems.Add(storeConfig.correspondingBagItem);

        // 新加入的武器，如果当前没有装备任何武器，自动装备
        if (bagItem.IsWeapon() && _equippedWeaponId < 0)
        {
            EquipWeapon(bagItem.GetConfig().weaponId);
        }
        else
        {
            // 刷新装备标识（确保新物品状态正确）
            RefreshEquipMarks();
        }

        Debug.Log($"[AddToBag] 生成成功！背包子物体数量 = {bagContentParent.childCount}，生成的物体 = {bagItem.name}");
    }

    /// <summary>
    /// 背包物品点击回调：如果是武器则装备
    /// </summary>
    private void OnBagItemClicked(BagItemConfig config)
    {
        if (config == null) return;

        if (config.weaponId >= 0)
        {
            // ===== 加这行日志 =====
            Debug.Log($"[BagAndStoreManager] 点击武器：{config.bagItemName} (weaponId={config.weaponId})，当前已装备={_equippedWeaponId}，是否相等={_equippedWeaponId == config.weaponId}");
            // ========================

            if (_equippedWeaponId == config.weaponId)
            {
                // ===== 加这行日志 =====
                Debug.Log($"[BagAndStoreManager] → 走 UnequipWeapon（取消装备，回退手枪）");
                // ========================
                UnequipWeapon();
            }
            else
            {
                // ===== 加这行日志 =====
                Debug.Log($"[BagAndStoreManager] → 走 EquipWeapon({config.weaponId})");
                // ========================
                EquipWeapon(config.weaponId);
            }
        }
        else
        {
            Debug.Log($"[BagAndStoreManager] 点击了非武器物品：{config.bagItemName}，暂未实现使用逻辑");
        }
    }


    /// <summary>
    /// 装备指定武器：更新静态选中ID + 刷新所有背包物品的装备标识
    /// </summary>
    public void EquipWeapon(int weaponId)
    {
        Debug.Log($"[BagAndStoreManager] EquipWeapon 被调用，weaponId={weaponId}");
        _equippedWeaponId = weaponId;
        // 写入跨场景静态字段，游戏场景的WeaponManager会读取
        WeaponManager.SelectedWeaponId = weaponId;
        Debug.Log($"[BagAndStoreManager] 已设置 WeaponManager.SelectedWeaponId={WeaponManager.SelectedWeaponId}");

        // 刷新所有背包物品的装备标识
        RefreshEquipMarks();

        // 如果当前场景有已配置好的WeaponManager且有武器挂载点，实时切换武器
        WeaponManager wm = FindObjectOfType<WeaponManager>();
        Debug.Log($"[BagAndStoreManager] 当前场景找到 WeaponManager={(wm == null ? "否" : "是")}，weaponHoldPoint={(wm == null || wm.weaponHoldPoint == null ? "空" : wm.weaponHoldPoint.name)}");
        if (wm != null && wm.weaponHoldPoint != null && wm.configList != null)
        {
            wm.SwitchWeapon(weaponId);
        }

        SaveToSave();
    }

    /// <summary>
    /// 刷新所有背包物品的装备标识：只有当前装备的武器显示isReady
    /// </summary>
    public void RefreshEquipMarks()
    {
        int equippedCount = 0;
        foreach (var item in _spawnedBagItems)
        {
            if (item == null) continue;
            bool isEquipped = item.IsWeapon() && item.GetConfig().weaponId == _equippedWeaponId;
            item.SetEquipped(isEquipped);
            if (isEquipped) equippedCount++;
        }
        Debug.Log($"[BagAndStoreManager] RefreshEquipMarks：_equippedWeaponId={_equippedWeaponId}，背包物品数={_spawnedBagItems.Count}，已装备标识数={equippedCount}");
    }

    /// <summary>
    /// 扫描背包容器下已有的BagItemView（编辑器中预先摆好的）
    /// 注册点击事件并加入列表，确保它们也能响应点击和显示装备标识
    /// </summary>
    private void ScanExistingBagItems()
    {
        if (bagContentParent == null) return;

        BagItemView[] existing = bagContentParent.GetComponentsInChildren<BagItemView>(true);
        foreach (var item in existing)
        {
            if (item == null) continue;
            if (_spawnedBagItems.Contains(item)) continue;

            // 【关键修复】先取消再注册，防止重复注册导致点击一次触发两次
            item.OnItemClicked -= OnBagItemClicked;
            item.OnItemClicked += OnBagItemClicked;

            _spawnedBagItems.Add(item);
            Debug.Log($"[ScanExistingBagItems] 注册已存在的背包物品：{item.name}");
        }
    }

    /// <summary>
    /// 从商店列表中移除指定商品卡片，后面的物品会被LayoutGroup自动往前推
    /// </summary>
    private void RemoveStoreItem(StoreItemConfig storeConfig)
    {
        StoreItemView target = spawnedStoreItems.Find(item => item.GetConfig() == storeConfig);
        if (target != null)
        {
            spawnedStoreItems.Remove(target);
            Destroy(target.gameObject);
        }
        else
        {
            Debug.LogWarning($"[BagAndStoreManager] 没找到商品 {storeConfig.itemName} 对应的卡片");
        }
    }
    public void LoadFromSave()
    {
        var save = SaveManager.Instance.Load<GameSaveData>();
        if (save == null) return;

        _purchasedItemIds.Clear();
        foreach (var id in save.purchasedItemIds)
            if (!string.IsNullOrEmpty(id)) _purchasedItemIds.Add(id);

        _purchasedBagItems.Clear();
        foreach (var bagId in save.purchasedBagItemIds)
        {
            var config = FindBagItemById(bagId);
            if (config != null && !_purchasedBagItems.Contains(config))
                _purchasedBagItems.Add(config);
        }

        // 【关键】从存档读取装备的武器ID，并同步给 WeaponManager
        _equippedWeaponId = save.equippedWeaponId;
        if (_equippedWeaponId >= 0)
        {
            WeaponManager.SelectedWeaponId = _equippedWeaponId;
        }
        else
        {
            WeaponManager.SelectedWeaponId = 0; // 未装备时默认手枪
        }
        Debug.Log($"[BagAndStoreManager] LoadFromSave：equippedWeaponId={_equippedWeaponId}，金币={save.totalCoin}，已购物品={save.purchasedItemIds.Count}，背包物品={save.purchasedBagItemIds.Count}");
    }


    public void SaveToSave()
    {
        var save = SaveManager.Instance.Load<GameSaveData>() ?? new GameSaveData();

        save.purchasedItemIds.Clear();
        foreach (var id in _purchasedItemIds) save.purchasedItemIds.Add(id);

        save.purchasedBagItemIds.Clear();
        foreach (var bag in _purchasedBagItems)
            if (bag != null && !string.IsNullOrEmpty(bag.bagItemId))
                save.purchasedBagItemIds.Add(bag.bagItemId);

        save.equippedWeaponId = _equippedWeaponId;
        SaveManager.Instance.Save(save);
        Debug.Log($"[BagAndStoreManager] SaveToSave：已保存 equippedWeaponId={_equippedWeaponId}，金币={save.totalCoin}，已购物品={save.purchasedItemIds.Count}，背包物品={save.purchasedBagItemIds.Count}");
    }

    private BagItemConfig FindBagItemById(string bagItemId)
    {
        if (string.IsNullOrEmpty(bagItemId) || itemConfigs == null) return null;
        foreach (var sc in itemConfigs)
            if (sc != null && sc.correspondingBagItem != null && sc.correspondingBagItem.bagItemId == bagItemId)
                return sc.correspondingBagItem;
        return null;
    }
    /// <summary>
    /// 取消装备武器：回退到默认手枪(id=0)，隐藏所有装备标识
    /// </summary>
    /// <summary>
    /// 取消装备武器：回退到默认手枪(id=0)，隐藏所有装备标识
    /// </summary>
    public void UnequipWeapon()
    {
        Debug.Log($"[BagAndStoreManager] UnequipWeapon 被调用，取消当前装备，回退默认手枪");
        _equippedWeaponId = -1;
        WeaponManager.SelectedWeaponId = 0;

        RefreshEquipMarks();

        WeaponManager wm = FindObjectOfType<WeaponManager>();
        if (wm != null && wm.weaponHoldPoint != null && wm.configList != null)
        {
            wm.SwitchWeapon(0);
        }

        // 【关键】取消装备后也要保存
        SaveToSave();
        Debug.Log($"[BagAndStoreManager] 已取消装备，已保存（equippedWeaponId=-1）");
    }


}
