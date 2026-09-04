using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : BaseMonoSingleton<UIManager>
{
    /// <summary>
    /// UI面板基类，所有UI脚本继承
    /// </summary>
    public class UIPanelBase : MonoBehaviour
    {
        public virtual void OnOpen()
        {
            gameObject.SetActive(true);
        }

        public virtual void OnClose()
        {
            gameObject.SetActive(false);
        }
    }
    private Stack<UIPanelBase> _uiStack = new Stack<UIPanelBase>();
    public Transform uiRoot;

    /// <summary>当前 UI 栈里的面板数量（外部判断栈是否为空用）</summary>
    public int StackCount => _uiStack.Count;

    /// <summary>
    /// 打开面板，压栈
    /// </summary>
    public void PushPanel(UIPanelBase panel)
    {
        if (panel == null) return;
        _uiStack.Push(panel);
        panel.OnOpen();
    }
    public void PopPanel() 
    {
        if (_uiStack.Count == 0) return;
        UIPanelBase panel = _uiStack.Pop();
        panel.OnClose();
    }
    /// <summary>
    /// 关闭全部UI
    /// </summary>
    public void PopAll()
    {
        while (_uiStack.Count > 0)
        {
            PopPanel();
        }
    }
    
}
