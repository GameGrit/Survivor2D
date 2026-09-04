/// <summary>
/// 怪物状态基类 —— 所有具体怪物状态继承此类
/// 设计原则：
///   1. 状态只做业务判断与切换，不直接操作Animator（通过CharacterAnimationComponent间接控制）
///   2. 移动逻辑放在OnFixedUpdate（物理更新），与Rigidbody2D对齐
///   3. 状态切换通过fsm.SwitchState()统一入口，禁止状态间直接互相引用
/// </summary>
public abstract class MonsterBaseState
{
    protected MonsterFsm fsm;

    public MonsterBaseState(MonsterFsm fsm)
    {
        this.fsm = fsm;
    }

    /// <summary>进入该状态时执行一次</summary>
    public abstract void OnEnter();

    /// <summary>每帧更新（逻辑判断、计时器）</summary>
    public abstract void OnUpdate();

    /// <summary>物理帧更新（Rigidbody移动、碰撞相关）</summary>
    public virtual void OnFixedUpdate() { }

    /// <summary>退出该状态时执行一次</summary>
    public abstract void OnExit();
}
