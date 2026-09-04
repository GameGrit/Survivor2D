using UnityEngine;

/// <summary>
/// 怪物有限状态机组件 —— 挂载在怪物GameObject上
/// 职责：
///   1. 持有所有状态实例，统一管理状态切换
///   2. 持有MonsterBase（数据/移动）与CharacterAnimationComponent（动画）引用
///   3. Update/FixedUpdate分发给当前状态
///   4. 提供ResetFSM供对象池复用调用
///
/// 与MonsterBase的关系：
///   - MonsterBase负责：血量、受击、死亡回收、移动方法
///   - MonsterFsm负责：状态流转、何时移动、何时播动画
///   - 两者通过引用互相协作，不重复实现逻辑
/// </summary>
public class MonsterFsm : MonoBehaviour
{
    [Header("组件引用（自动查找或手动拖）")]
    public MonsterBase monsterBase;
    public CharacterAnimationComponent animComp;


    public MonsterChaseState chaseState { get; private set; }
    public MonsterAttackState attackState { get; private set; }
    public MonsterDeathState deathState { get; private set; }


    private MonsterBaseState _currentState;

    // 状态机是否启用（死亡/回收时暂停更新）
    private bool _isActive = false;

    private void Awake()
    {
        // 自动查找缺失组件
        if (monsterBase == null)
            monsterBase = GetComponent<MonsterBase>();

        // 【关键】Animator可能在子对象上，所以用GetComponentInChildren
        if (animComp == null)
            animComp = GetComponentInChildren<CharacterAnimationComponent>();

        // 如果还是没有，在有Animator的对象上自动添加
        if (animComp == null)
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animComp = animator.gameObject.GetComponent<CharacterAnimationComponent>();
                if (animComp == null)
                {
                    animComp = animator.gameObject.AddComponent<CharacterAnimationComponent>();
                    Debug.LogWarning($"⚠️ 怪物 {gameObject.name} 未挂CharacterAnimationComponent，已在Animator所在对象自动添加");
                }
            }
            else
            {
                Debug.LogError($"❌ 怪物 {gameObject.name} 找不到Animator！动画将无法播放");
            }
        }

        chaseState = new MonsterChaseState(this);
        attackState = new MonsterAttackState(this);
        deathState = new MonsterDeathState(this);

    }

    private void Update()
    {
        if (!_isActive) return;

        // 全局暂停检查：暂停时不更新FSM逻辑（用 IsPaused 独立标志）
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        _currentState?.OnUpdate();
    }

    private void FixedUpdate()
    {
        if (!_isActive) return;

        // 全局暂停检查：暂停时不执行物理移动
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        _currentState?.OnFixedUpdate();
    }

    /// <summary>
    /// 状态切换唯一入口
    /// </summary>
    public void SwitchState(MonsterBaseState newState)
    {
        if (newState == null) return;

        _currentState?.OnExit();
        _currentState = newState;
        _currentState.OnEnter();
    }

    /// <summary>
    /// 对象池取出怪物时初始化FSM，直接进入追逐状态
    /// 【割草游戏设计】怪物生成即追玩家，不需要Idle待机检测
    /// 由MonsterBase.Init()调用
    /// </summary>
    public void InitFSM()
    {
        _isActive = true;
        animComp?.ResetAll();
        // 直接进入追逐状态，割草游戏怪物生成即追玩家
        SwitchState(chaseState);
    }

    /// <summary>
    /// 对象池回收时调用，完全停止FSM
    /// </summary>
    public void StopFSM()
    {
        _isActive = false;
        _currentState = null;
    }

    /// <summary>
    /// 获取当前状态名（调试用）
    /// </summary>
    public string GetCurrentStateName()
    {
        return _currentState?.GetType().Name ?? "None";
    }
}
