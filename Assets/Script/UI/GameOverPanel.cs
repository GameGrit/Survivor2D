using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    /// <summary>
    /// 游戏结束结算面板 —— 继承 UIManager.UIPanelBase，通过 UIManager 栈管理
    /// 监听 GameOverEventArgs，Push 自己到栈展示结算数据
    /// </summary>
    public class GameOverPanel : UIManager.UIPanelBase
    {
        [Header("面板根物体（不拖则控制自身 GameObject）")]
        public GameObject panelRoot;

        [Header("结算数据文本")]
        public TMP_Text surviveTimeText;   // 生存时间
        public TMP_Text killsText;         // 击杀数
        public TMP_Text levelText;         // 最终等级
        public TMP_Text waveText;          // 到达层数

        [Header("按钮")]
        public Button restartButton;       // 重新开始
        public Button quitButton;          // 退出游戏（可选）

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
            // 注册按钮事件
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);

            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);

            // 初始隐藏
            OnClose();
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<GameOverEventArgs>(OnGameOver);
        }

        private void OnDisable()
        {
            EventBus.Instance.Unsubscribe<GameOverEventArgs>(OnGameOver);
        }

        /// <summary>游戏结束事件回调 —— Push 自己到栈</summary>
        private void OnGameOver(GameOverEventArgs e)
        {
            // 先填充数据
            FillData(e);

            // 再 Push 到 UI 栈，OnOpen 会被调用显示面板
            UIManager.Instance.PushPanel(this);
        }

        /// <summary>填充结算数据</summary>
        private void FillData(GameOverEventArgs data)
        {
            // 生存时间格式化为 分:秒
            if (surviveTimeText != null)
            {
                int minutes = Mathf.FloorToInt(data.surviveTime / 60f);
                int seconds = Mathf.FloorToInt(data.surviveTime % 60f);
                surviveTimeText.text = $"生存时间  {minutes:00}:{seconds:00}";
            }

            if (killsText != null)
                killsText.text = $"击杀数  {data.totalKills}";

            if (levelText != null)
                levelText.text = $"最终等级  Lv.{data.playerLevel}";

            if (waveText != null)
                waveText.text = $"到达层数  第 {data.reachWave} 层";
        }

        /// <summary>重新开始按钮 —— Pop 出栈 + RestartGame</summary>
        private void OnRestartClicked()
        {
            UIManager.Instance.PopPanel();
            GameManager.Instance.RestartGame();
        }

        /// <summary>退出游戏按钮</summary>
        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
