/// <summary>
/// 玩家属性变化事件（强化、升级后属性变了抛这个，UI刷新用）
/// </summary>
public class PlayerStatsChangedEventArgs : BaseEventArgs
{
    public int level;          // 当前等级
    public int maxHp;          // 最大血量
    public int attackDamage;   // 攻击力
    public float moveSpeed;    // 移动速度
    public float attackSpeedMultiplier;

    public float bulletSpeed;  // 子弹速度
}
