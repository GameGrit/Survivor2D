using UnityEngine;

/// <summary>
/// 玩家动画控制 —— 轻量版，不使用FSM
/// 原因：割草游戏玩家只有「移动/待机」两个状态，攻击全自动，没有闪避/技能/硬直
/// 职责：
///   1. 监听PlayerController摇杆输入，有方向→走路，无方向→待机
///   2. 监听PlayerHealth.OnPlayerDie，死亡时播死亡动画
///   3. 通过CharacterAnimationComponent间接控制Animator，不直接操作Animator
///
/// 性能优化：
///   - 只在移动状态变化时调用SetMove，不每帧调用
///   - 死亡后禁用组件，停止更新
/// </summary>
public class PlayerAnim : MonoBehaviour
{
    [Header("组件引用（自动查找）")]
    public PlayerController playerController;
    public PlayerHealth playerHealth;
    public CharacterAnimationComponent animComp;
    SpriteRenderer spriteRenderer;
    // 上一帧是否在移动，用于变化检测
    private bool _wasMovingLastFrame = false;
    private bool _isDead = false;
    private bool _isflip = false;
    private void Awake()
    {
        // 自动查找缺失组件
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (animComp == null)
            animComp = GetComponent<CharacterAnimationComponent>();

        // 监听玩家死亡事件
        if (playerHealth != null)
        {
            playerHealth.OnPlayerDie += OnPlayerDie;
        }

    }

    private void Update()
    {
        if (_isDead) return;
        if (playerController == null || animComp == null) return;
        spriteRenderer = playerController.GetComponent<SpriteRenderer>();
        // 根据摇杆输入判断是否在移动
        bool isMoving = playerController.dir.sqrMagnitude > 0.01f;
        Vector2 direction = playerController.dir.normalized;
        if (Mathf.Abs(direction.x) > 0.01f)
        {
            if (direction.x < 0)
            {
                _isflip = true;
            }
            else
            {
                _isflip = false;
            }
        }
        spriteRenderer.flipX= _isflip;

        if (isMoving != _wasMovingLastFrame)
        {
            animComp.SetMove(isMoving);
            _wasMovingLastFrame = isMoving;
        }
    }

    /// <summary>
    /// 玩家死亡回调
    /// </summary>
    private void OnPlayerDie()
    {
        _isDead = true;
        animComp?.PlayDeath();
    }

    /// <summary>
    /// 新一局重置玩家动画（由GameManager/GameStartup调用）
    /// </summary>
    public void ResetPlayerAnim()
    {
        _isDead = false;
        _wasMovingLastFrame = false;
        animComp?.ResetAll();
        enabled = true;
    }

    private void OnDestroy()
    {
        // 取消事件订阅，防止内存泄漏
        if (playerHealth != null)
        {
            playerHealth.OnPlayerDie -= OnPlayerDie;
        }
    }
}
