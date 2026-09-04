using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 怪物头顶血条控制器：
///   - 默认隐藏
///   - 怪物受伤时调用 OnMonsterHurt() → 显示血条并刷新数值
///   - 显示 hideDelay 秒后自动隐藏
///   - 连续受伤会刷新隐藏倒计时（不会中途消失）
/// </summary>
public class MonsterHpBar : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("血条 Slider 组件")]
    public Slider hpBar;

    [Tooltip("关联的怪物基础脚本")]
    public MonsterBase hpBase;

    [Header("显示设置")]
    [Tooltip("受伤后血条持续显示多久后自动隐藏（秒），1~2秒之间")]
    public float hideDelay = 1.5f;

    // 延时隐藏协程的句柄，用于连续受伤时刷新倒计时
    private Coroutine _hideCoroutine;

    void Start()
    {
        // 默认不显示血条
        if (hpBar != null)
            hpBar.gameObject.SetActive(false);
    }

    /// <summary>
    /// 怪物受伤时由 MonsterBase.TakeDamage 调用：
    /// 刷新血条数值 → 显示 → 启动延时隐藏
    /// </summary>
    public void OnMonsterHurt()
    {
        if (hpBar == null || hpBase == null)
        {
            Debug.LogWarning($"[MonsterHpBar] {gameObject.name} 的 hpBar 或 hpBase 未赋值！");
            return;
        }

        // 【关键】血量比例 = 当前血量 / 最大血量（满血=1，空血=0）
        // 注意：不是 最大/当前，那样数值会大于1且方向反了
        float ratio = hpBase.CurrentHp / hpBase.maxHp;
        hpBar.value = Mathf.Clamp01(ratio);

        // 显示血条
        hpBar.gameObject.SetActive(true);

        // 连续受伤时：取消上一次的隐藏倒计时，重新计时
        if (_hideCoroutine != null)
            StopCoroutine(_hideCoroutine);

        _hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    /// <summary>
    /// 延时隐藏协程
    /// </summary>
    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);
        hpBar.gameObject.SetActive(false);
        _hideCoroutine = null;
    }

    /// <summary>
    /// 对象池回收 / 怪物死亡时手动隐藏血条
    /// </summary>
    public void HideImmediately()
    {
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }
        if (hpBar != null)
            hpBar.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        // 对象池回收时确保协程停止、血条隐藏，避免复用时残留状态
        HideImmediately();
    }
}
