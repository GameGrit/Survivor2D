using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    /// <summary>
    /// 暂停面板 —— 继承 UIManager.UIPanelBase，通过 UIManager 栈管理
    /// 
    /// 【面板按钮】
    ///   - 设置：Push 设置面板到栈（设置面板盖在暂停面板上）
    ///   - 继续游戏：PopAll 清空栈 + ResumeGame
    ///   - 主菜单：先保存进度，再 PopAll 清空栈 + 加载主菜单场景
    /// 
    /// 【栈流程】
    ///   暂停按钮 → Push(PausePanel) → 栈: [PausePanel]
    ///   点设置 → Push(SettingPanel) → 栈: [PausePanel, SettingPanel]
    ///   设置点返回 → Pop() → 栈: [PausePanel]，设置面板关闭，回到暂停面板
    ///   点继续 → PopAll() → 栈空，恢复游戏
    /// </summary>
    public class PausePanel : UIManager.UIPanelBase
    {
        [Header("面板根物体（不拖则控制自身 GameObject）")]
        public GameObject panelRoot;
        // 防循环：标记当前暂停是否由面板自己触发
        private bool _pauseTriggeredByPanel = false;
        [Header("按钮")]
        public Button settingButton;    // 设置
        public Button resumeButton;     // 继续游戏
        public Button quitButton;       // 主菜单
        public Button pauseButton;

        [Header("设置面板引用（点设置按钮时 Push 到栈）")]
        public GameObject settingPanel;

        [Header("主菜单场景名（必须是 Addressables 分组中的场景名）")]
        public string mainMenuSceneName = "StartScene";

        // override OnOpen/OnClose，控制 panelRoot 而不是自身
        public override void OnOpen()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            else gameObject.SetActive(true);

            // 面板一显示，如果游戏还没暂停，就自动触发暂停
            if (GameManager.Instance != null && !GameManager.Instance.IsPaused)
            {
                _pauseTriggeredByPanel = true;   // 标记：是我自己触发的
                GameManager.Instance.PauseGame();
                _pauseTriggeredByPanel = false;  // 调用完立刻复位
            }
        }

        public override void OnClose()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            else gameObject.SetActive(false);
        }

        private void Awake()
        {
            // 初始隐藏
            OnClose();
            // 注册按钮事件
            if (settingButton != null)
                settingButton.onClick.AddListener(OnSettingClicked);

            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);

            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);
            pauseButton.onClick.AddListener(() =>
            {
                OnOpen();
            });

        }

        private void OnEnable()
        {
            // 监听暂停/恢复事件
            EventBus.Instance.Subscribe<GamePausedEventArgs>(OnGamePaused);
        }

        private void OnDisable()
        {
            EventBus.Instance.Unsubscribe<GamePausedEventArgs>(OnGamePaused);
        }

        /// <summary>
        /// 游戏暂停/恢复事件回调
        /// 暂停时 Push 自己到栈，恢复时 PopAll 清空栈
        /// </summary>
        private void OnGamePaused(GamePausedEventArgs e)
        {
            if (e.isPaused)
            {
                // 如果是面板自己触发的暂停，就不要重复Push了（否则死循环）
                if (_pauseTriggeredByPanel) return;

                // 外部触发的暂停（按P键、切后台自动暂停），才Push面板显示
                UIManager.Instance.PushPanel(this);
            }
            else
            {
                // 恢复游戏：清空栈，所有面板关掉
                UIManager.Instance.PopAll();
            }
        }


        /// <summary>设置按钮 —— Push 设置面板到栈（设置面板盖在暂停面板上，暂停面板保持激活）</summary>
        private void OnSettingClicked()
        {
            if (settingPanel != null)
            {
                SettingPanel set = settingPanel.GetComponent<SettingPanel>();
                if (set != null)
                {
                    // 只 Push 到栈，PushPanel 内部会自动调用 set.OnOpen() 显示设置面板
                    // 不要禁用暂停面板自己：设置面板可能是子物体，且返回时需要恢复
                    UIManager.Instance.PushPanel(set);
                }
                else
                {
                    Debug.LogWarning("[PausePanel] settingPanel 上没有 SettingPanel 组件");
                }
            }
            else
            {
                Debug.LogWarning("[PausePanel] 未配置设置面板引用");
            }
        }

        /// <summary>继续游戏按钮 —— 调用 ResumeGame，内部会重置 IsPaused/timeScale 并发事件，事件里 PopAll 关面板</summary>
        private void OnResumeClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResumeGame();
            }
            OnClose();
        }

        /// <summary>主菜单按钮 —— 先保存进度，再清空栈 + 加载主菜单场景</summary>
        private void OnQuitClicked()
        {
            // 【关键修复】返回主菜单前先保存本局进度，否则等级/经验/层数会丢
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveCurrentRun();
                Debug.Log("[PausePanel] 返回主菜单前已保存本局进度");
            }

            // 恢复时间流速
            Time.timeScale = 1f;

            // 清空 UI 栈
            UIManager.Instance.PopAll();

            // 切回主菜单BGM
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBgm(BgmType.MainMenu);

            // 【Addressables 改造】场景已打包到 Remote_Scenes 组，必须用 Addressables 异步加载
            // 不能再用 SceneManager.LoadScene(mainMenuSceneName)
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                AddressablesManager.Instance.LoadSceneAsync(mainMenuSceneName, null, error =>
                {
                    Debug.LogError("[PausePanel] 加载主菜单场景失败：" + error);
                });
            }
            else
            {
                Debug.LogWarning("[PausePanel] 未配置主菜单场景名，无法返回");
            }
        }
    }
}
