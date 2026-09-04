using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 玩家HUD控制器 - 左上角血量条、经验条、等级显示
/// 使用 Slider 组件控制进度，监听 EventBus 事件自动刷新
/// 
/// 支持的动态变化：
/// 1. 血量变化（受伤/回血）→ 血条实时更新
/// 2. 血量上限变化（强化加最大血）→ 血条 maxValue + value 同步更新
/// 3. 经验变化（捡经验球）→ 经验条实时更新
/// 4. 经验上限变化（升级后下一级需要更多经验）→ 经验条 maxValue + value 自动重算
/// 5. 等级变化 → 等级文字更新
/// 
/// 【修复记录】
/// - 事件订阅从 OnEnable 移到 Awake，防止 ContinueSavedRun 发布事件时还没订阅
/// - Start 里加延迟一帧刷新，确保切场景后数据恢复完再刷新UI
/// - RefreshAll 改成 public，供 GameManager.ContinueSavedRun 主动调用
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    public TextMeshProUGUI coinText;
    [Header("【可选】等级经验配置（拖进来初始化更准确）")]
    public LevelProgressionConfig progressionConfig;

    [Header("血量条 (Slider)")]
    public Slider hpSlider;          // 血条 Slider 组件
    public TMP_Text hpText;          // 血量文字 "当前/最大"（可选）

    [Header("经验条 (Slider)")]
    public Slider expSlider;         // 经验条 Slider 组件
    public TMP_Text expText;         // 经验文字 "当前/需要"（可选）
    public TMP_Text levelText;       // 等级文字 "Lv.X"

    [Header("右上角层数显示")]
    public TMP_Text waveText;        // 层数文字 "第 X 层"

    private void Awake()
    {
        // 【关键修复】订阅移到 Awake，比 Start 更早执行
        // 防止 ContinueSavedRun 在 Start 之前发布事件，导致 HUD 收不到、数字不显示
        EventBus.Instance.Subscribe<CoinChangedEventArgs>(OnCoinChanged);
        EventBus.Instance.Subscribe<PlayerHpChangedEventArgs>(OnHpChanged);
        EventBus.Instance.Subscribe<AddExpEventArgs>(OnExpAdded);
        EventBus.Instance.Subscribe<PlayerLevelUpEventArgs>(OnLevelUp);
        EventBus.Instance.Subscribe<PlayerStatsChangedEventArgs>(OnStatsChanged);
        EventBus.Instance.Subscribe<WaveChangedEventArgs>(OnWaveChanged);
    }

    private void OnEnable()
    {
        // 订阅已在 Awake 里做了，这里留空
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<CoinChangedEventArgs>(OnCoinChanged);
        EventBus.Instance.Unsubscribe<PlayerHpChangedEventArgs>(OnHpChanged);
        EventBus.Instance.Unsubscribe<AddExpEventArgs>(OnExpAdded);
        EventBus.Instance.Unsubscribe<PlayerLevelUpEventArgs>(OnLevelUp);
        EventBus.Instance.Unsubscribe<PlayerStatsChangedEventArgs>(OnStatsChanged);
        EventBus.Instance.Unsubscribe<WaveChangedEventArgs>(OnWaveChanged);
    }

    private void Start()
    {
        // 启动时先刷新一次
        RefreshAll();
        // 【关键修复】延迟一帧再刷新一次
        // 确保 ContinueSavedRun 恢复完数据后，HUD 能拿到最新值
        // 解决切场景后数字不显示的问题
        StartCoroutine(DelayedRefresh());
    }

    private System.Collections.IEnumerator DelayedRefresh()
    {
        yield return null; // 等一帧
        RefreshAll();
        Debug.Log("[PlayerHUD] 延迟一帧刷新完成，UI已更新");
    }

    /// <summary>
    /// 全量刷新一次UI（从 PlayerExp 拿初始数据）
    /// 【public】供 GameManager.ContinueSavedRun 主动调用
    /// </summary>
    public void RefreshAll()
    {
        PlayerExp p = PlayerExp.Instance;
        if (p == null)
        {
            Debug.LogWarning("[PlayerHUD] PlayerExp 不存在，UI 无法初始化");
            return;
        }
        UpdateCoin(CoinManager.Instance.CurrentCoin);

        // 1. 刷新等级
        UpdateLevel(p.level);

        // 2. 刷新经验条
        int nextRequire = 0;
        if (progressionConfig != null)
        {
            nextRequire = progressionConfig.CalcNextLevelRequire(p.level);
        }
        else
        {
            // 没拖配置的话，先默认1，等第一次加经验时会自动修正
            nextRequire = 1;
        }
        UpdateExpBar(p.curExp, nextRequire);

        // 3. 刷新血条（初始是满血）
        UpdateHpBar(p.maxHp, p.maxHp);

        // 4. 刷新层数
        UpdateWave(WaveSystem.Instance.currentWave);
    }

    #region 事件回调 - 数据变化时自动触发
    void OnCoinChanged(CoinChangedEventArgs e) => UpdateCoin(e.newCoin);
    void UpdateCoin(int coin) { if (coinText != null) coinText.text = coin.ToString(); }
    /// <summary>
    /// 血量变化时刷新血条（受伤/回血/加血上限都会触发）
    /// </summary>
    void OnHpChanged(PlayerHpChangedEventArgs e)
    {
        UpdateHpBar(e.currentHp, e.maxHp);
    }

    /// <summary>
    /// 获得经验时刷新经验条
    /// 事件里已经带了当前等级所需的经验上限，自动适配
    /// </summary>
    void OnExpAdded(AddExpEventArgs e)
    {
        UpdateExpBar(e.currentExp, e.nextLevelRequireExp);
    }

    /// <summary>
    /// 升级时刷新等级 + 经验条（经验上限会变！）
    /// </summary>
    void OnLevelUp(PlayerLevelUpEventArgs e)
    {

        UpdateLevel(e.newLevel);
        // 升级后经验上限是新等级的上限，自动用新值
        UpdateExpBar(e.remainExp, e.nextRequireExp);
    }

    /// <summary>
    /// 属性变化时同步UI（双保险）
    /// 比如选了加血上限的强化，虽然 Heal 已经会触发 HpChanged，
    /// 但这里再同步一次确保万无一失
    /// </summary>
    void OnStatsChanged(PlayerStatsChangedEventArgs e)
    {
        // 同步血量（找 PlayerHealth 拿最新值）
        PlayerHealth health = GameObject.FindWithTag("Player")?.GetComponent<PlayerHealth>();
        if (health != null)
        {
            float currentHp = e.maxHp * health.GetHpRatio();
            UpdateHpBar(currentHp, e.maxHp);
        }

        // 同步等级
        UpdateLevel(e.level);

        // 同步经验条（初始化时 curExp=0，需要刷新一次避免停留在 0/0）
        int nextRequire = 1;
        if (ExpSystem.Instance != null)
        {
            nextRequire = ExpSystem.Instance.GetConfig().CalcNextLevelRequire(e.level);
        }
        UpdateExpBar(PlayerExp.Instance.curExp, nextRequire);
    }

    /// <summary>
    /// 层数变化时刷新右上角显示
    /// </summary>
    void OnWaveChanged(WaveChangedEventArgs e)
    {
        UpdateWave(e.newWave);
    }

    #endregion

    #region UI更新方法 - 纯UI逻辑，不碰业务数据

    /// <summary>
    /// 更新血条显示
    /// </summary>
    /// <param name="current">当前血量</param>
    /// <param name="max">最大血量</param>
    public void UpdateHpBar(float current, float max)
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = max;   // 上限会变（加血强化）
            hpSlider.value = current;
        }

        if (hpText != null)
        {
            hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    /// <summary>
    /// 更新经验条显示
    /// </summary>
    /// <param name="current">当前经验</param>
    /// <param name="require">升级所需经验（每级不一样！）</param>
     void UpdateExpBar(int current, int require)
    {
        if (expSlider != null && require > 0)
        {
            expSlider.maxValue = require;  // 上限每级都变
            expSlider.value = current;
        }

        if (expText != null)
        {
            expText.text = $"{current} / {require}";
        }
    }

    /// <summary>
    /// 更新等级文字
    /// </summary>
    void UpdateLevel(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"Lv.{level}";
        }
    }

    /// <summary>
    /// 更新层数文字（右上角）
    /// </summary>
    void UpdateWave(int wave)
    {
        if (waveText != null)
        {
            waveText.text = $"第 {wave} 层";
        }
    }

    #endregion
}
