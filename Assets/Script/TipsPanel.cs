using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TipsPanel : UIManager.UIPanelBase
{
    // ========== 新增：两个事件，给外部订阅 ==========
    public event Action OnConfirm;   // 点确定
    public event Action OnCancel;    // 点取消
    public TextMeshProUGUI tipsTxt;
    public Button btnCancel;
    public Button btnOk;

    // 游戏结束模式标志：true时确定按钮执行重新开始，false时走原来的商店逻辑
    private bool _isGameOverMode = false;

    // 缓存确定按钮的默认位置（恢复时用）
    private Vector2 _btnOkDefaultPos;
    private bool _defaultPosCached = false;

    private void Start()
    {
        // 缓存确定按钮默认位置（第一次Start时记录）
        if (!_defaultPosCached && btnOk != null)
        {
            _btnOkDefaultPos = btnOk.GetComponent<RectTransform>().anchoredPosition;
            _defaultPosCached = true;
        }

        RectTransform rt = btnCancel.GetComponent<RectTransform>();

        OnClose();
        if (btnCancel != null)
        {
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(() =>
            {
                OnCancel?.Invoke();
                OnClose();
                rt.anchoredPosition = new Vector2(-288, 0);
                btnOk.gameObject.SetActive(true);
            });
        }
        if (btnOk != null)
        {
            btnOk.onClick.RemoveAllListeners();
            btnOk.onClick.AddListener(() =>
            {
                // 游戏结束模式：点确定 → 重开游戏
                if (_isGameOverMode)
                {
                    ResetButtonsToDefault();
                    OnClose();
                    GameManager.Instance.RestartGame();
                    return;
                }

                // 正常模式（商店购买）：走原来的逻辑
                OnConfirm?.Invoke();
                AdjuestButtonPosition();
            });

        }

    }
    public void AdjuestButtonPosition()
    {
        btnOk.gameObject.SetActive(false);

        RectTransform rt = btnCancel.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(7, 0);
    }
    public void Show(string message)
    {
        // 确保不是游戏结束模式
        _isGameOverMode = false;
        ResetButtonsToDefault();

        if (tipsTxt != null)
            tipsTxt.text = message;
        // 每次弹出时恢复确定按钮的显示和位置，否则第二次购买时按钮不可见
        if (btnOk != null)
        {
            btnOk.gameObject.SetActive(true);
            RectTransform rt = btnCancel.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-288, 0);
        }
        OnOpen();
    }

    /// <summary>
    /// 游戏结束专用：显示"游戏结束"，隐藏取消按钮，确定按钮居中
    /// 点确定 → 重新开始游戏
    /// </summary>
    public void ShowGameOver()
    {
        _isGameOverMode = true;

        // 1. 改文字
        if (tipsTxt != null)
            tipsTxt.text = "游戏结束";

        // 2. 隐藏取消按钮
        if (btnCancel != null)
            btnCancel.gameObject.SetActive(true);


        // 3. 确定按钮显示并居中
        if (btnOk != null)
        {
            btnOk.gameObject.SetActive(false);

            RectTransform rt =btnCancel.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(10, 0); // 居中
 
        }
        GameManager.Instance.PauseGame();
        // 4. 弹出面板
        OnOpen();
        btnCancel.onClick.RemoveAllListeners();
        btnCancel.onClick.AddListener(() =>
        {
            // 【Addressables 改造】场景已打包到 Remote_Scenes 组，必须用 Addressables 异步加载
            // 不能再用 SceneManager.LoadSceneAsync("StartScene")
            AddressablesManager.Instance.LoadSceneAsync("StartScene", null, error =>
            {
                Debug.LogError("[TipsPanel] 加载 StartScene 失败：" + error);
            });
        });
    }

    /// <summary>
    /// 恢复按钮到默认状态（取消显示、确定回到原位）
    /// 游戏结束重开、关闭面板时调用，防止影响下一次商店使用
    /// </summary>
    private void ResetButtonsToDefault()
    {
        _isGameOverMode = false;

        if (btnCancel != null)
            btnCancel.gameObject.SetActive(true);

        if (btnOk != null)
        {
            btnOk.gameObject.SetActive(true);
            RectTransform rt = btnOk.GetComponent<RectTransform>();
            // 如果缓存了默认位置就恢复，否则不动（防止第一次还没缓存就被改）
            if (_defaultPosCached)
                rt.anchoredPosition = _btnOkDefaultPos;
        }

        // 取消按钮也恢复默认位置
        if (btnCancel != null)
        {
            RectTransform rt = btnCancel.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-288, 0);
        }
    }
}
