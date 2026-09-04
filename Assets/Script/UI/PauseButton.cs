using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 暂停按钮 —— 挂在右上角暂停按钮上
/// 点击后调用 GameManager.PauseGame()，PausePanel 会自动弹出
/// 
/// 【使用方法】
///   1. 在 HUD Canvas 下建一个 Button
///   2. 把本脚本挂在按钮上
///   3. 不需要拖任何引用，按钮的 onClick 会在 Awake 自动绑定
/// </summary>
[RequireComponent(typeof(Button))]
public class PauseButton : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(OnPauseClicked);
        }
        else
        {
            Debug.LogError("[PauseButton] 找不到 Button 组件！请确保脚本挂在 UI Button 上", this);
        }
    }

    private void OnPauseClicked()
    {
        // 直接调用 PauseGame，内部会判断 CurrentState 是否为 Playing
        // 不在这里做状态检查，避免切场景后状态不对导致点了没反应
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseGame();
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnPauseClicked);
        }
    }
}
