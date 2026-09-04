using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 启动加载面板 —— 驱动 Addressables 初始化/检查更新/下载流程
/// 
/// 【企业标准】
///   1. 成功 → 跳转下一场景（通过 Addressables 异步加载）
///   2. 失败 → 显示错误文字 + 重试按钮，绝不卡死
///   3. 所有 UI 引用都做 null 检查，没拖也不会报空引用
///   
/// 【Addressables 改造说明】
///   场景已打包到 Remote_Scenes 组，不再放在 Build Settings 里。
///   因此跳转场景必须用 AddressablesManager.LoadSceneAsync，不能用 SceneManager.LoadScene。
/// </summary>
public class LoadDataPanel : MonoBehaviour
{
    [Header("进度 UI")]
    public Slider downloadSlider;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI statusText;

    [Header("失败 UI（可选，没拖则只打日志）")]
    public TextMeshProUGUI errorText;
    public Button retryButton;

    [Header("下载完成后跳转的场景名（必须是 Addressables 分组中的场景名）")]
    public string nextSceneName = "StartScene";

    void Start()
    {
        // 隐藏失败 UI
        if (retryButton != null) retryButton.gameObject.SetActive(false);
        if (errorText != null) errorText.gameObject.SetActive(false);

        StartLoad();
    }

    /// <summary>
    /// 开始加载流程（重试时也调这个）
    /// </summary>
    private void StartLoad()
    {
        // 重置 UI 状态
        if (retryButton != null) retryButton.gameObject.SetActive(false);
        if (errorText != null) errorText.gameObject.SetActive(false);
        if (downloadSlider != null) downloadSlider.gameObject.SetActive(true);
        if (downloadSlider != null) downloadSlider.value = 0f;

        AddressablesManager.Instance.InitAndDownload(
            status => { if (statusText != null) statusText.text = status; },
            progress =>
            {
                if (downloadSlider != null) downloadSlider.value = progress;
                if (progressText != null) progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
            },
            () =>
            {
                Debug.Log("[LoadDataPanel] 下载完成，准备通过 Addressables 跳转场景：" + nextSceneName);

                // 跳转前先隐藏加载界面，防止场景切换过程中残留挡住画面
                gameObject.SetActive(false);

                // 【Addressables 改造】用 Addressables 异步加载场景，不再用 SceneManager.LoadScene
                AddressablesManager.Instance.LoadSceneAsync(
                    nextSceneName,
                    () => Debug.Log("[LoadDataPanel] 场景 " + nextSceneName + " 加载完成"),
                    error =>
                    {
                        Debug.LogError("[LoadDataPanel] 场景加载失败：" + error);
                        // 加载失败时重新显示面板，让用户能看到错误并重试
                        gameObject.SetActive(true);
                        if (statusText != null) statusText.text = "场景加载失败";
                        if (errorText != null)
                        {
                            errorText.text = error;
                            errorText.gameObject.SetActive(true);
                        }
                        if (retryButton != null)
                        {
                            retryButton.gameObject.SetActive(true);
                            retryButton.onClick.RemoveAllListeners();
                            retryButton.onClick.AddListener(StartLoad);
                        }
                    }
                );
            },
            error =>
            {
                Debug.LogError("[LoadDataPanel] 加载失败：" + error);
                // 关键：把错误信息直接显示到进度条上方的状态文本，用户一眼就能看到失败原因
                if (statusText != null)
                {
                    statusText.text = "加载失败：" + error;
                }
                // 百分比文本显示失败状态，不会停在100%让用户猜
                if (progressText != null)
                {
                    progressText.text = "失败";
                }
                if (errorText != null)
                {
                    errorText.text = error;
                    errorText.gameObject.SetActive(true);
                }
                if (retryButton != null)
                {
                    retryButton.gameObject.SetActive(true);
                    // 防止重复绑定
                    retryButton.onClick.RemoveAllListeners();
                    retryButton.onClick.AddListener(StartLoad);
                }
            }
        );
    }
}
