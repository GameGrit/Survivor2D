using UnityEngine;

public class PlayerExp : BaseMonoSingleton<PlayerExp>
{
    protected override void Awake()
    {
        base.Awake();
        Debug.Log($"[PlayerExp] Awake，实例ID={GetInstanceID()}，场景={gameObject.scene.name}，物体名={gameObject.name}，activeSelf={gameObject.activeSelf}，当前等级={level}，当前经验={curExp}");
    }

    [Header("等级经验")]
    public int level;
    public int curExp;
    [Header("吸血")]
    [Tooltip("吸血比例（0.05=造成伤害的5%回血）")]
    public float lifeStealRate = 0f;

    [Header("拾取范围")]
    [Tooltip("经验球/金币自动吸附范围（基础2，加卡片后增大）")]
    public float pickupRange = 2f;
    [Header("本局战斗属性（升级会改这些）")]
    public int maxHp;           // 最大血量
    public int attackDamage;    // 攻击力
    public float moveSpeed;     // 移动速度
    [Header("攻速系数（1=正常，0.8=快20%，升级卡片乘这个）")]
    public float attackSpeedMultiplier = 1f;
    public float bulletSpeed;   // 子弹飞行速度

    /// <summary>每局开局重置本局数据</summary>
    public void ResetPlayerForNewGame(int startMaxHp, int startAttack, float startMoveSpeed, float startAttackSpeedMultiplier, float startBulletSpeed)

    {
        level = 1;
        curExp = 0;
        lifeStealRate = 0f;

        maxHp = startMaxHp;
        attackDamage = startAttack;
        moveSpeed = startMoveSpeed;
        attackSpeedMultiplier = startAttackSpeedMultiplier;

        bulletSpeed = startBulletSpeed;
        pickupRange = 2f;

        Debug.Log($"[PlayerExp] ResetPlayerForNewGame，等级重置为{level}，实例ID={GetInstanceID()}");

        // 初始化完成后抛事件，通知 HUD 刷新等级/经验/属性（解决 Start 时序导致 HUD 读到全 0 的问题）
        EventBus.Instance.Publish(new PlayerStatsChangedEventArgs()
        {
            level = level,
            maxHp = maxHp,
            attackDamage = attackDamage,
            moveSpeed = moveSpeed,
            attackSpeedMultiplier = attackSpeedMultiplier,

            bulletSpeed = bulletSpeed
        });
    }
}
