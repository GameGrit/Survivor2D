using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 怪物基础组件：血量、向玩家移动、死亡回调、接触伤害
/// 【FSM集成说明】
///   - 移动由 canMove 开关控制，FSM的ChaseState开启，Idle/Attack/Death关闭
///   - 死亡不再直接回收，而是通知FSM进入DeathState，播完死亡动画再回收
///   - 所有动画通过 CharacterAnimationComponent 播放，本组件不直接碰Animator
/// </summary>
public class MonsterBase : MonoBehaviour
{
    [Tooltip("死亡掉落金币数（0=不掉金币）")]
    public int coinDrop = 10;

    [Header("怪物基础属性")]
    [Tooltip("最大血量（预制体上的值会覆盖这个默认值）")]
    public float maxHp = 30f;

    [Tooltip("移动速度（单位/秒），割草游戏普通怪建议1.5~2.5")]
    public float moveSpeed = 1.5f;

    [Tooltip("死亡掉落经验值")]
    public int expDrop = 5;

    [Tooltip("接触玩家造成的基础伤害（实际伤害由WaveSystem按层数缩放并Clamp到上限）")]
    public float contactDamage = 10f;

    [Header("FSM状态切换范围")]
    public float attackRange = 2.0f;   // 攻击范围：进入此距离切Attack

    [Header("死亡动画")]
    public float deathDuration = 0.7f; // 死亡动画时长，与美术Clip对齐

    [Header("朝向翻转")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("精灵原图默认朝向：true=朝右（flipX=false时脸朝右），false=朝左（flipX=false时脸朝左）。两个怪物精灵方向不同时分别设置即可")]
    public bool defaultFacingRight = true;

    private float _currentHp;
    private float _baseMoveSpeed; // 缓存原始移速，SetDifficulty时基于此计算，避免对象池复用后累乘
    private float _currentDamage; // 当前层实际伤害（SetDifficulty时计算，含层数缩放和上限Clamp）
    private Transform _playerTrans;
    private Rigidbody2D _rb2D;
    private Collider2D _collider2D;
    public GameObject Prefab = null;

    // FSM引用（自动查找）
    private MonsterFsm _fsm;

    // 移动开关：由FSM控制，true时FixedUpdate才执行移动
    private bool _canMove = false;

    // 死亡标志：防止重复触发Die()（怪物死后还被打会反复进Die）
    private bool _isDead = false;

    private static int _monsterCounter; // 怪物编号计数器
    private int _monsterId; // 当前怪物编号

    //怪物死亡回调事件
    public System.Action<MonsterBase> OnMonsterDie;

    /// <summary>
    /// 当前血量（只读，供血条等外部组件读取）
    /// </summary>
    public float CurrentHp => _currentHp;

    /// <summary>
    /// 当前层实际伤害（只读，调试用）
    /// </summary>
    public float CurrentDamage => _currentDamage;

    private void Awake()
    {
        _rb2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();

        // 【防御性】自动补全MonsterFsm，预制体没挂也能跑
        _fsm = GetComponent<MonsterFsm>();
        if (_fsm == null)
        {
            _fsm = gameObject.AddComponent<MonsterFsm>();
            Debug.LogWarning($"⚠️ 怪物 {gameObject.name} 未挂MonsterFsm，已自动添加");
        }

        _monsterId = _monsterCounter++; // 每个怪物分配一个唯一编号
        _baseMoveSpeed = moveSpeed; // 缓存原始移速
        _currentDamage = contactDamage; // 初始化时用基础值，SetDifficulty时会重算
    }

    /// <summary>
    /// 对象池取出怪物时初始化
    /// </summary>
    public void Init(Transform playerTransform)
    {
        // 【兜底】如果传入的玩家引用是null（切场景后MonsterSpawner缓存的引用失效），自己重新找
        // 这样不管从哪里生成的怪物，都能自己找到玩家，不会出现"玩家引用null不移动"的问题
        if (playerTransform == null)
        {
            GameObject playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null)
            {
                playerTransform = playerGo.transform;
                Debug.LogWarning($"[MonsterBase] {gameObject.name} 传入玩家引用为null，已自动重新查找玩家");
            }
            else
            {
                Debug.LogError($"[MonsterBase] {gameObject.name} 找不到 Tag=Player 的物体！怪物不会追人");
            }
        }

        _playerTrans = playerTransform;
        _currentHp = maxHp;
        _canMove = false;
        gameObject.SetActive(true);

        // 启用碰撞器（回收时可能被禁用）
        SetColliderEnable(true);

        // 初始化FSM，从追逐状态开始（割草游戏怪生成即追人）
        if (_fsm != null)
        {
            _fsm.InitFSM();
            //Debug.Log($"✅ 怪物 {gameObject.name} 初始化完成，FSM状态={_fsm.GetCurrentStateName()}，玩家引用={(_playerTrans != null ? "OK" : "NULL")}");
        }
        else
        {
            Debug.LogError($"❌ 怪物 {gameObject.name} 的FSM为null，无法追人！");
        }
    }

    private void FixedUpdate()
    {
        // 只有FSM允许移动时才执行移动逻辑
        if (_canMove)
        {
            MoveToPlayer();
            UpdateFaceDirection();
        }
    }

    /// <summary>
    /// 朝玩家方向移动
    /// </summary>
    void MoveToPlayer()
    {
        // 运行时自动恢复玩家引用（切场景后引用可能丢失）
        EnsurePlayerReference();

        if (_playerTrans == null)
        {
            Debug.LogWarning($" 怪物 {gameObject.name} 的玩家引用是null！不移动");
            return;
        }

        Vector2 dir = (_playerTrans.position - transform.position).normalized;
        _rb2D.velocity = dir * moveSpeed;
    }

    /// <summary>
    /// 怪物受到伤害
    /// </summary>
    public void TakeDamage(float damage)
    {
        _currentHp -= damage;
        // 【吸血】玩家造成伤害时按比例回血
        if (PlayerExp.Instance != null && PlayerExp.Instance.lifeStealRate > 0f)
        {
            int healAmount = Mathf.RoundToInt(damage * PlayerExp.Instance.lifeStealRate);
            if (healAmount > 0)
            {
                PlayerHealth health = FindObjectOfType<PlayerHealth>();
                if (health != null)
                {
                    health.Heal(healAmount);
                }
            }
        }

        // 显示伤害数字
        DamageNumberManager.Instance.ShowDamage(transform.position, damage, isPlayerHurt: false);

        // 触发头顶血条显示（子物体上挂了 MonsterHpBar 才会响应）
        GetComponent<MonsterHpBar>()?.OnMonsterHurt();

        if (_currentHp <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 怪物死亡 —— 掉经验、加击杀、回调、切FSM死亡状态
    /// 【解耦】停移动/关碰撞/播死亡动画/计时回收 全部由 MonsterDeathState 负责
    /// </summary>
    void Die()
    {
        // 防止重复触发死亡（怪物死后碰撞器没及时关时，被打多次会反复进Die）
        if (_isDead) return;
        _isDead = true;

        Debug.Log($"[MonsterBase] 💀 怪物死亡！name={gameObject.name}，ID={_monsterId}");

        // 死亡时立即隐藏头顶血条
        GetComponentInChildren<MonsterHpBar>()?.HideImmediately();

        // 掉落经验球（按层数倍率计算实际经验值）
        if (expDrop > 0)
        {
            int realExp = Mathf.RoundToInt(expDrop * WaveSystem.Instance.GetExpFactor());
            Manager.ExpOrbManager.Instance.SpawnExpOrb(transform.position, realExp,coinDrop);
        }

        // 上报击杀数
        GameManager.Instance.AddKill();

        // 通知层数系统有怪物死亡
        WaveSystem.Instance.OnMonsterKilled();

        //触发死亡回调（Spawner里减存活计数）
        OnMonsterDie?.Invoke(this);

        // 切到FSM死亡状态：DeathState负责停移动、关碰撞、播死亡动画，
        // 动画播完后发布 MonsterDeathAnimationFinishedEventArgs，由Spawner回对象池
        if (_fsm != null)
        {
            _fsm.SwitchState(_fsm.deathState);
        }
        else
        {
            // FSM不存在时兜底直接回收
            RecycleToPool();
        }
    }

    /// <summary>
    /// 回收对象池（由MonsterDeathState动画播完后调用）
    /// </summary>
    public void RecycleToPool()
    {
        PoolManager.Instance.Recycle(Prefab, gameObject);
    }

    /// <summary>
    /// 对象池回收时调用
    /// </summary>
    private void OnDisable()
    {
        OnMonsterDie = null;
        if (_rb2D != null)
            _rb2D.velocity = Vector2.zero;

        // 停止FSM，防止回收后状态机还在跑
        _fsm?.StopFSM();

        // 重置死亡标志，否则对象池复用后怪物一出来就是死的
        _isDead = false;
    }

    /// <summary>设置难度系数，缩放血量、速度和伤害</summary>
    /// <param name="hpFactor">血量倍率</param>
    /// <param name="speedFactor">速度倍率</param>
    /// <param name="damageFactor">伤害倍率（实际伤害还会被WaveSystem的maxMonsterDamage上限Clamp）</param>
    internal void SetDifficulty(float hpFactor, float speedFactor = 1f, float damageFactor = 1f)
    {
        _currentHp = maxHp * hpFactor;
        // 基于原始移速计算，避免对象池复用后累乘导致越来越快
        moveSpeed = _baseMoveSpeed * speedFactor;

        // 伤害 = 基础伤害 × 层数倍率，然后 Clamp 到硬上限
        // 用 WaveSystem.CalculateActualDamage 统一计算，保证上限逻辑集中管理
        if (WaveSystem.Instance != null)
        {
            _currentDamage = WaveSystem.Instance.CalculateActualDamage(contactDamage);
        }
        else
        {
            // WaveSystem 不存在时兜底：直接乘倍率不 Clamp
            _currentDamage = contactDamage * damageFactor;
        }

        Debug.Log($"[MonsterBase] {gameObject.name} 难度设置：血量={_currentHp:F0}，移速={moveSpeed:F2}，伤害={_currentDamage:F1}（基础={contactDamage}）");
    }

    /// <summary>
    /// 根据玩家位置左右翻面（flipX，不影响碰撞）
    /// 【兼容不同初始朝向】通过 defaultFacingRight 字段适配朝右/朝左精灵
    /// </summary>
    void UpdateFaceDirection()
    {
        if (_playerTrans == null || spriteRenderer == null)
            return;

        // 玩家在怪物左边 → 怪物应该朝左；玩家在右边 → 应该朝右
        bool playerAtLeft = _playerTrans.position.x < transform.position.x;

        // 默认朝右的精灵：朝左需要flipX=true；默认朝左的精灵：朝左需要flipX=false
        spriteRenderer.flipX = defaultFacingRight ? playerAtLeft : !playerAtLeft;
    }

    // ==================== FSM调用的公共接口 ====================

    /// <summary>FSM控制移动开关</summary>
    public void SetMoveEnable(bool enable)
    {
        _canMove = enable;
        if (!enable && _rb2D != null)
        {
            _rb2D.velocity = Vector2.zero;
        }
    }

    /// <summary>FSM控制碰撞器开关（死亡时关闭防止继续接触伤害）</summary>
    public void SetColliderEnable(bool enable)
    {
        if (_collider2D != null)
            _collider2D.enabled = enable;
    }

    /// <summary>获取与玩家的距离（FSM状态切换用）</summary>
    public float GetDistanceToPlayer()
    {
        // 运行时自动恢复玩家引用
        EnsurePlayerReference();

        if (_playerTrans == null) return float.MaxValue;
        return Vector2.Distance(transform.position, _playerTrans.position);
    }

    /// <summary>
    /// 确保玩家引用有效，丢失了就重新查找
    /// 【为什么需要】切场景后MonsterSpawner缓存的玩家引用可能失效，
    /// 或者对象池复用的怪物玩家引用丢失，这里运行时自动恢复，不会再出现float.MaxValue
    /// </summary>
    private void EnsurePlayerReference()
    {
        // 不仅检查null，还要检查玩家对象是否还活跃（防止指向已销毁的旧玩家）
        // 切场景后旧玩家被Destroy，但Unity的==null有时序延迟，必须加activeInHierarchy检查
        if (_playerTrans != null
            && _playerTrans.gameObject != null
            && _playerTrans.gameObject.activeInHierarchy)
        {
            return; // 引用有效，不用重新找
        }

        // 引用无效，重新查找
        GameObject playerGo = GameObject.FindWithTag("Player");
        if (playerGo != null)
        {
            _playerTrans = playerGo.transform;
            Debug.Log($"[MonsterBase] {gameObject.name} 玩家引用已恢复 → {playerGo.name}");
        }
        else
        {
            Debug.LogError($"[MonsterBase] {gameObject.name} 找不到 Tag=Player 的物体！请检查玩家对象的Tag是否设置为Player");
        }
    }

    /// <summary>
    /// 对玩家造成接触伤害（由攻击动画的 Animation Event 在命中帧调用）
    /// 【关键】动画事件触发时必须再次确认玩家在攻击范围内，
    /// 因为攻击动画播放期间玩家可能已走开，不能隔空扣血
    /// </summary>
    public void DealContactDamage()
    {
        if (_playerTrans == null) return;

        // 动画命中帧触发伤害时，再次确认玩家是否还在攻击范围内
        float distance = Vector2.Distance(transform.position, _playerTrans.position);
        if (distance > attackRange)
        {
            Debug.Log($"[MonsterBase] {gameObject.name} 攻击命中帧时玩家已走开（距离={distance:F2} > 范围={attackRange}），不扣血");
            return;
        }

        PlayerHealth playerHealth = _playerTrans.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            // 用当前层实际伤害（已含层数缩放和上限Clamp）
            playerHealth.TakeDamage(_currentDamage);
        }
    }

}
