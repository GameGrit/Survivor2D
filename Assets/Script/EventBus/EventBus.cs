using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

/// <summary>
/// 事件参数基类 2026.8.7
/// </summary>
public class BaseEventArgs
{
}

public class EventBus : BaseSingleton<EventBus>
{
    private Dictionary<Type, Delegate> eventDic = new Dictionary<Type, Delegate>();
    public delegate void EventDelegate<T>(T e) where T : BaseEventArgs;
    /// <summary>
    /// 注册监听
    /// </summary>
    public void Subscribe<T>(EventDelegate<T> eventDelegate) where T : BaseEventArgs
    {
        Type t = typeof(T);
        if (eventDic.ContainsKey(t))
        {
            eventDic[t] = Delegate.Combine(eventDic[t], eventDelegate);
        }
        else
        {
            eventDic.Add(typeof(T), eventDelegate);
        }
    }
    public void Unsubscribe<T>(EventDelegate<T> eventDelegate) where T : BaseEventArgs
    {
        Type t = typeof(T);
        if (eventDic.ContainsKey(t))
        {
            eventDic[t] = Delegate.Remove(eventDic[t], eventDelegate);
        }
    }
    public void Publish<T>(T e) where T : BaseEventArgs
    {
        Type t =typeof(T);
        if (eventDic.TryGetValue(t,out Delegate del))
        {

            if (del is EventDelegate<T> act)
            {
                act.Invoke(e);
            }
        }
    }
    public void ClearAll()
    {
        eventDic.Clear();
    }
}

