using UnityEngine;

/// <summary>
/// 怪物追逐状态
/// 行为：朝玩家移动，播放走路动画；
///       距离玩家超出追击范围 → 回Idle；
///       进入攻击范围 → 切Attack（当前怪物是接触伤害，AttackState可预留）
/// </summary>
public class MonsterChaseState : MonsterBaseState
{
    public MonsterChaseState(MonsterFsm fsm) : base(fsm) { }

    public override void OnEnter()
    {
        // 开启移动
        fsm.monsterBase.SetMoveEnable(true);
        // 播放走路动画
        fsm.animComp.SetMove(true);
    }

    public override void OnUpdate()
    {
        float distance = fsm.monsterBase.GetDistanceToPlayer();
        // 【调试日志】确认OnUpdate是否在执行、距离和攻击范围的实际值
        //Debug.Log($"[ChaseDebug] 怪物={fsm.monsterBase.name} 距离玩家={distance:F2} 攻击范围={fsm.monsterBase.attackRange} 当前状态={fsm.GetCurrentStateName()}");

        if (fsm.monsterBase.attackRange >= distance)
        {
            Debug.Log($"[ChaseDebug] ✅ 满足攻击条件，切换到AttackState！");
            fsm.SwitchState(fsm.attackState);
        }
    }

    public override void OnFixedUpdate()
    {
        // 物理帧执行移动（由MonsterBase.MoveToPlayer实现）
        // SetMoveEnable=true时，MonsterBase.FixedUpdate会自动调用移动
    }

    public override void OnExit()
    {
        fsm.animComp.SetMove(false);
    }
}
