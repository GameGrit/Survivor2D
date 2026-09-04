using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 音频设置面板 —— 继承 UIManager.UIPanelBase
/// 
/// 【兼容两种打开方式】
///   1. UIManager.PushPanel 打开（游戏场景从暂停面板进入）
///   2. 直接 SetActive(true) 打开（主菜单场景从 StartPanel 进入）
///   
/// 两种方式都会触发 OnEnable，所以音量同步放在 OnEnable 里最稳妥。
/// 返回按钮也兼容两种方式：栈里有就 Pop，没有就直接关自己 + 显示 StartPanel。
/// </summary>
public class SettingPanel : UIManager.UIPanelBase
{
    [Header("面板根物体（不拖则控制自身 GameObject）")]
    public GameObject panelRoot;

    [Header("音量滑块")]
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("音量数值文本（可选，显示百分比）")]
    public TMP_Text bgmValueText;
    public TMP_Text sfxValueText;

    [Header("关闭/返回按钮")]
    public Button btnClose;

    // override OnOpen/OnClose，控制 panelRoot
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
        // 绑定关闭按钮
        if (btnClose != null)
        {
            btnClose.onClick.AddListener(OnCloseClicked);
        }

        // 绑定滑块事件（实时生效）
        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        // 初始隐藏
        OnClose();
    }

    private void OnEnable()
    {
        // 不管是 Push 打开还是直接 SetActive 打开，都会触发 OnEnable
        // 在这里同步音量，确保两种方式都能同步
        SyncUIFromAudioManager();
    }

    /// <summary>
    /// 从 AudioManager 读取当前音量，刷新滑块和文本
    /// </summary>
    private void SyncUIFromAudioManager()
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[SettingPanel] AudioManager 不存在，无法同步音量");
            return;
        }

        // 临时取消事件订阅，避免 SetValue 时触发回调造成回环
        UnregisterSliderEvents();

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.value = AudioManager.Instance.BgmVolume;
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = AudioManager.Instance.SfxVolume;

        UpdateValueTexts();

        RegisterSliderEvents();
    }

    private void OnBgmVolumeChanged(float value)
    {
        AudioManager.Instance?.SetBgmVolume(value);
        UpdateValueTexts();
    }

    private void OnSfxVolumeChanged(float value)
    {
        AudioManager.Instance?.SetSfxVolume(value);
        UpdateValueTexts();
    }

    /// <summary>刷新百分比文本</summary>
    private void UpdateValueTexts()
    {
        if (bgmValueText != null && bgmVolumeSlider != null)
            bgmValueText.text = Mathf.RoundToInt(bgmVolumeSlider.value * 100f) + "%";
        if (sfxValueText != null && sfxVolumeSlider != null)
            sfxValueText.text = Mathf.RoundToInt(sfxVolumeSlider.value * 100f) + "%";
    }

    /// <summary>
    /// 关闭/返回按钮 —— 兼容两种打开方式
    ///   - 栈里有面板：Pop 出栈，回到上一个面板（暂停面板）
    ///   - 栈空（直接 SetActive 打开的，主菜单场景）：直接关自己 + 显示 StartPanel
    /// </summary>
    private void OnCloseClicked()
    {
        // 先尝试 Pop 出栈
        if (UIManager.Instance != null && UIManager.Instance.StackCount > 0)
        {
            UIManager.Instance.PopPanel();
        }
        else
        {
            // 栈空，说明是直接 SetActive 打开的（主菜单场景）
            // 直接关自己
            OnClose();

            // 显示 StartPanel（主菜单场景）
            StartPanel startPanel = FindAnyObjectByType<StartPanel>(FindObjectsInactive.Include);
            if (startPanel != null)
            {
                startPanel.gameObject.SetActive(true);
            }
        }
    }

    // 事件注册/反注册，防止 Sync 时回环
    private void RegisterSliderEvents()
    {
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
    }

    private void UnregisterSliderEvents()
    {
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
    }
}
