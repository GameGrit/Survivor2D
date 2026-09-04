using System.Collections;
using UnityEngine;

/// <summary>
/// 怪物死亡状态 —— FSM终态，与Chase/Attack平级
/// 行为：停止移动、关闭碰撞、播放死亡动画；动画播完后发布事件通知外部回收
///
/// 【为什么用FSM状态而不是独立MonoBehaviour】
///   - 死亡是怪物生命周期的一个状态，应纳入FSM统一管理
///   - 状态切换走SwitchState唯一入口，OnEnter/OnExit生命周期一致
///   - 协程通过fsm（MonoBehaviour）启动，不依赖额外组件
///
/// 【解耦边界】
///   - 本状态只负责播死亡动画和计时
///   - 动画播完后发布 MonsterDeathAnimationFinishedEventArgs
///   - 由 MonsterSpawner 监听事件执行对象池回收
/// </summary>
public class MonsterDeathState : MonsterBaseState
{
    private float _deathDuration;
    private Coroutine _deathCoroutine;

    public MonsterDeathState(MonsterFsm fsm) : base(fsm)
    {
    }

    public override void OnEnter()
    {
        // 从MonsterBase读取配置的死亡时长，保持Inspector配置入口统一
        _deathDuration = fsm.monsterBase.deathDuration;

        // 死亡状态：停移动、关碰撞、播死亡动画
        fsm.monsterBase.SetMoveEnable(false);
        fsm.monsterBase.SetColliderEnable(false);
        fsm.animComp?.PlayDeath();

        // 协程通过fsm（继承MonoBehaviour）启动
        _deathCoroutine = fsm.StartCoroutine(WaitDeathAnimation());
    }

    public override void OnUpdate()
    {
        // 死亡状态是终态，不需要每帧逻辑判断
    }

    public override void OnExit()
    {
        // 正常情况下死亡状态不会主动退出（对象直接被回收）
        // 防御：如果被强制切换（如对象池复用前重置），停止未完成的协程
        if (_deathCoroutine != null)
        {
            fsm.StopCoroutine(_deathCoroutine);
            _deathCoroutine = null;
        }
    }

    /// <summary>
    /// 等待死亡动画时长，结束后发布"动画播完"事件
    /// </summary>
    private IEnumerator WaitDeathAnimation()
    {
        yield return new WaitForSeconds(_deathDuration);

        // 发布死亡动画完成事件，由 MonsterSpawner 监听并回对象池
        EventBus.Instance.Publish(new MonsterDeathAnimationFinishedEventArgs
        {
            monster = fsm.monsterBase
        });

        _deathCoroutine = null;
    }
}
