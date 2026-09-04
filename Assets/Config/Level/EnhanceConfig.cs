using UnityEngine;

public enum EnhanceType
{
    AddAttack,      // 加攻击力
    AddMaxHp,       // 加最大血量
    AddMoveSpeed,   // 加移动速度
    AddAttackSpeed, // 加攻击速度（减少攻击间隔）
    AddBulletSpeed,  // 加子弹速度
    AddPickupRange,  // 拾取范围
    AddLifeSteal//吸血
}

[CreateAssetMenu(fileName = "EnhanceConfig", menuName = "Configs/EnhanceConfig")]
public class EnhanceConfig : ScriptableObject
{
    public EnhanceType enhanceType;
    public string showName;
    [TextArea] public string desc;
    [Header("拾取范围专属")]
    [Tooltip("拾取范围加成")]

    public float pickupRangeValue = 1f;
    [Header("数值增益")]
    public int addAttackValue;
    public int addMaxHpValue;
    public float addMoveSpeedValue;
    public float attackCdScale;   // 攻击间隔乘这个系数，0.85=减少15%间隔
    public float addBulletSpeedValue;
    [Header("吸血专属")]
    [Tooltip("吸血比例（0.05=造成伤害的5%回血）")]
    public float lifeStealRate = 0.05f;
    public Sprite sprite;
}
