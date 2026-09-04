using UnityEngine;

/// <summary>
/// 通用角色动画组件 —— 玩家与怪物共用
/// 职责：封装Animator操作，对外提供语义化接口；FSM/业务层永远不直接碰Animator
/// 性能优化：
///   1. 参数名预转Hash，避免每帧字符串查找
///   2. 只在值变化时才Set参数，减少Animator内部状态脏标记
///   3. 支持对象池重置（ResetAll），防止复用实例残留动画状态
/// </summary>
[RequireComponent(typeof(Animator))]
public class CharacterAnimationComponent : MonoBehaviour
{
    [Header("动画参数名（与Animator控制器一致）")]
    public string moveParamName = "IsMove";
    public string attackTriggerName = "Trigger_Attack";
    public string hurtTriggerName = "Trigger_Hurt";
    public string deathTriggerName = "Trigger_Death";

    private Animator _animator;

    // 缓存的参数Hash
    private int _moveHash;
    private int _attackHash;
    private int _hurtHash;
    private int _deathHash;

    // 参数是否存在于AnimatorController中（防御性检查，避免每帧报"Parameter does not exist"）
    private bool _moveParamExists;
    private bool _attackParamExists;
    private bool _hurtParamExists;
    private bool _deathParamExists;

    // 上一次的值，用于变化检测，避免重复Set
    private bool _lastMoveValue = false;
    private bool _isDead = false;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        CacheParameterHashes();
        ValidateParameters();
    }

    /// <summary>
    /// 预缓存所有Animator参数Hash（企业标准：禁止每帧传字符串）
    /// </summary>
    private void CacheParameterHashes()
    {
        _moveHash = Animator.StringToHash(moveParamName);
        _attackHash = Animator.StringToHash(attackTriggerName);
        _hurtHash = Animator.StringToHash(hurtTriggerName);
        _deathHash = Animator.StringToHash(deathTriggerName);
    }

    /// <summary>
    /// 验证参数是否存在于AnimatorController中
    /// 【为什么需要】不同怪物可能用不同的AnimatorController，参数名可能不一致，
    /// 如果直接SetBool/SetTrigger会每帧报"Parameter 'Hash xxx' does not exist"
    /// </summary>
    private void ValidateParameters()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[CharacterAnimationComponent] {gameObject.name} 没有Animator或Controller，动画将被禁用");
            _moveParamExists = _attackParamExists = _hurtParamExists = _deathParamExists = false;
            return;
        }

        // 遍历Animator的所有参数，检查是否存在
        foreach (var param in _animator.parameters)
        {
            if (param.nameHash == _moveHash) _moveParamExists = true;
            if (param.nameHash == _attackHash) _attackParamExists = true;
            if (param.nameHash == _hurtHash) _hurtParamExists = true;
            if (param.nameHash == _deathHash) _deathParamExists = true;
        }

        // 打印缺失的参数，方便美术/策划排查
        if (!_moveParamExists)
            Debug.LogWarning($"[CharacterAnimationComponent] {gameObject.name} 的Animator缺少参数 '{moveParamName}'（移动动画将不生效）");
        if (!_attackParamExists)
            Debug.LogWarning($"[CharacterAnimationComponent] {gameObject.name} 的Animator缺少参数 '{attackTriggerName}'（攻击动画将不生效）");
        if (!_hurtParamExists)
            Debug.LogWarning($"[CharacterAnimationComponent] {gameObject.name} 的Animator缺少参数 '{hurtTriggerName}'（受击动画将不生效）");
        if (!_deathParamExists)
            Debug.LogWarning($"[CharacterAnimationComponent] {gameObject.name} 的Animator缺少参数 '{deathTriggerName}'（死亡动画将不生效）");

    }

    /// <summary>
    /// 设置移动/待机动画
    /// 性能优化：仅当值发生变化时才调用SetBool
    /// </summary>
    public void SetMove(bool isMoving)
    {
        if (_isDead || !_moveParamExists) return;
        if (_lastMoveValue == isMoving) return; // 值没变，跳过，减少Animator开销

        _animator.SetBool(_moveHash, isMoving);
        _lastMoveValue = isMoving;
    }

    /// <summary>
    /// 播放攻击动画（Trigger型，触发一次即自动消费）
    /// </summary>
    public void PlayAttack()
    {
        if (_isDead)
        {
            Debug.LogWarning($"[AnimDebug] {gameObject.name} PlayAttack 被跳过：_isDead=true");
            return;
        }
        if (!_attackParamExists)
        {
            Debug.LogWarning($"[AnimDebug] {gameObject.name} PlayAttack 被跳过：_attackParamExists=false（Animator缺少参数 '{attackTriggerName}'）");
            return;
        }
        if (_animator == null)
        {
            Debug.LogError($"[AnimDebug] {gameObject.name} PlayAttack 被跳过：_animator==null");
            return;
        }
        Debug.Log($"[AnimDebug] {gameObject.name} 触发攻击动画 SetTrigger({attackTriggerName})，当前状态={_animator.GetCurrentAnimatorClipInfo(0).Length}");
        _animator.SetTrigger(_attackHash);
    }

    /// <summary>
    /// 播放受击动画
    /// </summary>
    public void PlayHurt()
    {
        if (_isDead || !_hurtParamExists) return;
        _animator.SetTrigger(_hurtHash);
    }

    /// <summary>
    /// 播放死亡动画；调用后锁定组件，禁止再切换其他动画
    /// </summary>
    public void PlayDeath()
    {
        if (_isDead) return;
        _isDead = true;

        if (_deathParamExists)
            _animator.SetTrigger(_deathHash);

        // 死亡时强制停在移动=false，防止死亡动画被移动状态打断
        if (_moveParamExists)
        {
            _animator.SetBool(_moveHash, false);
            _lastMoveValue = false;
        }
    }

    /// <summary>
    /// 对象池取出实例时调用：重置所有动画参数到初始状态
    /// 【关键】不重置会导致上一个死亡怪物复活后直接播死亡动画
    /// </summary>
    public void ResetAll()
    {
        _isDead = false;
        _lastMoveValue = false;

        // 清空所有未消费的Trigger（防止粘包）
        if (_attackParamExists) _animator.ResetTrigger(_attackHash);
        if (_hurtParamExists) _animator.ResetTrigger(_hurtHash);
        if (_deathParamExists) _animator.ResetTrigger(_deathHash);

        if (_moveParamExists)
            _animator.SetBool(_moveHash, false);

        // 回到Layer 0 默认状态（通常是Idle）
        _animator.Play("Idle", 0, 0f);
    }

    /// <summary>
    /// 获取当前Animator（仅调试用，业务层不要直接用）
    /// </summary>
    public Animator GetAnimator() => _animator;
}
