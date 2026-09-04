using Unity.VisualScripting;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("受伤无敌时间（秒）")]
    public float invincibleTime = 0.5f;
    private float _invincibleTimer;

    public float _currentHp;

    public System.Action OnPlayerDie;

    private void Awake()
    {
        // 保底初始化：如果 PlayerExp 还没被初始化（maxHp=0），就给默认值
        // 这样即使场景里没挂 GameStartup，游戏也能跑
        if (PlayerExp.Instance.maxHp <= 0)
        {
            Debug.LogWarning("[PlayerHealth] PlayerExp 未初始化，使用默认值保底");
            PlayerExp.Instance.ResetPlayerForNewGame(
                startMaxHp: 100,
                startAttack: 10,
                startMoveSpeed: 3f,
                startAttackSpeedMultiplier: 1f,
                startBulletSpeed: 8f
            );

            // 同步移速给 PlayerController
            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.moveSpeed = PlayerExp.Instance.moveSpeed;
            }
        }


    }

    private void Start()
    {
        // 从属性中心拿初始最大血量
        _currentHp = PlayerExp.Instance.maxHp;
        // 启动时刷新一次血条UI
        PublishHpChangedEvent();

    }

    private void Update()
    {
        if (_invincibleTimer > 0)
        {
            _invincibleTimer -= Time.deltaTime;
        }


    }

    public void TakeDamage(float damage)
    {
        if (_invincibleTimer > 0)
            return;

        _currentHp -= damage;
        _invincibleTimer = invincibleTime;



        DamageNumberManager.Instance.ShowDamage(transform.position, damage, isPlayerHurt: true);

        // 抛血量变化事件，HUD刷新血条
        PublishHpChangedEvent();

        if (_currentHp <= 0)
        {
            _currentHp = 0;
            Debug.Log($"[PlayerHealth] 💀 玩家死亡！");
            OnPlayerDie?.Invoke();



            TipsPanel tipsPanel = FindObjectOfType<TipsPanel>(includeInactive: true);
            if (tipsPanel != null)
            {
                tipsPanel.ShowGameOver();
                Debug.Log("[PlayerHealth] 已调用 TipsPanel.ShowGameOver()");
            }
            else
                Debug.LogError("[PlayerHealth] 场景里找不到 TipsPanel 脚本！请确认 Canvas 下挂了 TipsPanel 组件");

            // 通知 GameManager 游戏结束（内部会 FreezeWorld 停游戏）
            GameManager.Instance.GameOver();
        }
    }



    public void Heal(float amount)
    {
        _currentHp = Mathf.Min(_currentHp + amount, PlayerExp.Instance.maxHp);
        // 抛血量变化事件，HUD刷新血条
        PublishHpChangedEvent();
    }

    /// <summary>重置血量（新一局用）</summary>
    public void ResetHp()
    {
        _currentHp = PlayerExp.Instance.maxHp;
        _invincibleTimer = 0;
        // 重置后也刷新一次UI
        PublishHpChangedEvent();
    }

    /// <summary>设置当前血量（恢复存档用）</summary>
    public void SetHp(float hp)
    {
        _currentHp = Mathf.Clamp(hp, 0f, PlayerExp.Instance.maxHp);
        PublishHpChangedEvent();
    }

    /// <summary>发布血量变化事件</summary>
    private void PublishHpChangedEvent()
    {
        float maxHp = PlayerExp.Instance.maxHp;
        if (maxHp <= 0) maxHp = 1; // 防止除以0

        EventBus.Instance.Publish(new PlayerHpChangedEventArgs()
        {
            currentHp = _currentHp,
            maxHp = maxHp,
            hpRatio = _currentHp / maxHp
        });

    }

    /// <summary>外部获取当前血量比例，UI血条用</summary>
    public float GetHpRatio()
    {
        float maxHp = PlayerExp.Instance.maxHp;
        if (maxHp <= 0) return 1f;
        return _currentHp / maxHp;
    }
}
