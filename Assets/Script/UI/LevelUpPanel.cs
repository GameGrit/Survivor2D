using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 升级三选一面板 —— 继承 UIManager.UIPanelBase，通过 UIManager 栈管理
/// 监听 PlayerLevelUpEventArgs，Push 自己到栈展示三选一
/// </summary>
public class LevelUpPanel : UIManager.UIPanelBase
{
    [Header("面板根物体（不拖则控制自身 GameObject）")]
    public GameObject panelRoot;

    [Header("强化配置列表（从这里随机抽）")]
    public List<EnhanceConfig> allEnhanceConfigs;

    [Header("UI引用")]
    public Button[] optionButtons;         // 3个选项按钮
    public TMP_Text[] optionNameTexts;     // 3个选项的名称文字
    public TMP_Text[] optionDescTexts;     // 3个选项的描述文字
    public Image[] optionImages;         // 3个选项的sprite

    public TMP_Text levelText;

    // 当前抽到的3个强化配置
    private List<EnhanceConfig> _currentOptions = new List<EnhanceConfig>();

    // override OnOpen/OnClose
    public override void OnOpen()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        else gameObject.SetActive(true);
    }

    public override void OnClose()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        else gameObject.SetActive(false);
    }

    private void Awake()
    {
        // 事件订阅必须在 Awake 里做
        EventBus.Instance.Subscribe<PlayerLevelUpEventArgs>(OnPlayerLevelUp);

        // 注册按钮点击事件
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            optionButtons[i].onClick.AddListener(() => OnSelectOption(index));
        }

        // 一开始隐藏
        OnClose();
    }

    private void OnDestroy()
    {
        EventBus.Instance.Unsubscribe<PlayerLevelUpEventArgs>(OnPlayerLevelUp);
    }

    /// <summary>玩家升级了，弹出面板</summary>
    void OnPlayerLevelUp(PlayerLevelUpEventArgs e)
    {
        // 每5级才弹出升级面板
        if (e.newLevel % 5 != 0) return;
        Debug.Log($"[LevelUpPanel] 🎉 收到升级事件！newLevel={e.newLevel}，弹出选择面板");
        ShowPanel(e.newLevel);
    }

    /// <summary>显示面板 —— 先初始化数据，再 Push 到栈</summary>
    public void ShowPanel(int newLevel)
    {
        levelText.text = $"升到 {newLevel} 级！";

        // 升级时回满血
        PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.ResetHp();

        // 随机抽3个不同的强化
        RandomOptions();
        UpdateOptionUI();

        // Push 到 UI 栈，OnOpen 会被调用显示面板
        UIManager.Instance.PushPanel(this);

        // 静默暂停（IsPaused + timeScale 同步，确保武器/怪物FSM都停住；不发事件，防止暂停面板叠出来）
        GameManager.Instance.PauseGame(silent: true);
    }

    /// <summary>随机抽3个强化选项</summary>
    void RandomOptions()
    {
        _currentOptions.Clear();

        if (allEnhanceConfigs == null || allEnhanceConfigs.Count == 0)
        {
            Debug.LogError("[LevelUpPanel] 强化配置列表是空的！请在Inspector里拖入EnhanceConfig");
            return;
        }

        List<EnhanceConfig> tempList = new List<EnhanceConfig>(allEnhanceConfigs);
        int count = Mathf.Min(3, tempList.Count);

        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, tempList.Count);
            _currentOptions.Add(tempList[randomIndex]);
            tempList.RemoveAt(randomIndex);
        }
    }

    /// <summary>更新选项UI显示</summary>
    void UpdateOptionUI()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (i < _currentOptions.Count)
            {
                optionButtons[i].gameObject.SetActive(true);
                optionImages[i].sprite = _currentOptions[i].sprite;
                optionNameTexts[i].text = _currentOptions[i].showName;
                optionDescTexts[i].text = _currentOptions[i].desc;
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>玩家选了某个选项 —— Pop 出栈 + 应用强化 + 恢复游戏</summary>
    void OnSelectOption(int index)
    {
        if (index < 0 || index >= _currentOptions.Count) return;

        // 应用强化
        EnhanceSystem.Instance.ApplyEnhance(_currentOptions[index]);

        // Pop 出栈，OnClose 会被调用隐藏面板
        UIManager.Instance.PopPanel();

        // 静默恢复（IsPaused + timeScale 同步恢复；不发事件，防止 PopAll 把其他面板也关了）
        GameManager.Instance.ResumeGame(silent: true);
    }
}
