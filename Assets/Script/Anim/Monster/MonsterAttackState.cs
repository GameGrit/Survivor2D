using UnityEngine;

/// <summary>
/// 怪物攻击状态
/// 行为：停止移动，播放攻击动画；按攻击间隔循环触发攻击；
///       玩家超出攻击范围 → 切回 ChaseState
/// </summary>
public class MonsterAttackState : MonsterBaseState
{
    [Tooltip("攻击间隔（秒），两次攻击动画之间的冷却")]
    private float _attackInterval = 1.0f;
    private float _attackTimer;

    [Tooltip("攻击状态最小停留时间（秒），防止玩家移动导致状态在Attack/Chase间抖动，攻击动画被打断")]
    private float _minStayDuration = 0.6f;
    private float _stayTimer;

    public MonsterAttackState(MonsterFsm fsm) : base(fsm)
    {
    }

    public override void OnEnter()
    {
        Debug.Log($"✅ 进入攻击状态！怪物={fsm.monsterBase.name}");
        // 攻击状态下停止移动，站定攻击
        fsm.monsterBase.SetMoveEnable(false);
        // 停止走路动画
        fsm.animComp.SetMove(false);
        // 立即播第一次攻击
        _attackTimer = 0f;
        _stayTimer = 0f;
        fsm.animComp.PlayAttack();
    }

    public override void OnUpdate()
    {
        _stayTimer += Time.deltaTime;
        _attackTimer += Time.deltaTime;

        // 攻击冷却计时，到点了再播下一次攻击动画
        // 【伤害改为动画事件触发】攻击动画的命中帧通过 Animation Event 调用 MonsterBase.DealContactDamage()
        // 这样伤害与动画命中帧对齐，不需要代码里硬编码扣血时机
        if (_attackTimer >= _attackInterval)
        {
            _attackTimer = 0f;
            fsm.animComp.PlayAttack();
        }

        // 【防抖动】至少停留_minStayDuration秒后才检查退出条件
        // 原因：玩家移动会导致距离在attackRange边界反复横跳，状态刚进就退，攻击动画永远播不出来
        if (_stayTimer >= _minStayDuration)
        {
            float distance = fsm.monsterBase.GetDistanceToPlayer();
            // 玩家走出攻击范围 → 切回追逐
            if (fsm.monsterBase.attackRange < distance)
            {
                fsm.SwitchState(fsm.chaseState);
                return;
            }
        }
    }

    public override void OnExit()
    {
        // 退出攻击状态时恢复移动（玩家走出攻击范围后继续追）
        fsm.monsterBase.SetMoveEnable(true);
    }
}
