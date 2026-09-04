using UnityEngine;

[CreateAssetMenu(fileName = "武器配置", menuName = "Game/WeaponConfig")]
public class WeaponConfig : ScriptableObject
{
    [Header("基础信息")]
    public string weaponName;
    public int weaponId;

    [Header("显示资源")]
    public GameObject weaponPrefab;       //武器预制体（实例化到挂载点）
    public GameObject bulletPrefab;

    [Header("【位移修正】武器本地位置偏移")]
    [Tooltip("不同武器预制体pivot不同，用这个微调挂载位置，解决切换时跳动问题")]
    public Vector3 localPositionOffset = Vector3.zero;

    [Header("战斗属性")]
    public float damage;
    public float fireInterval;        //攻击间隔（射速）
    public float bulletSpeed;

    [Header("武器类型区分")]
    public WeaponFireType fireType;

    [Header("射击音效（不同武器可配不同音效）")]
    [Tooltip("在 AudioConfig 的 SfxType 枚举中选；默认 PlayerShoot")]
    public SfxType fireSfx = SfxType.PlayerShoot;

    [Header("【散弹专属】仅fireType=Shotgun生效")]
    public int pelletCount = 6;
    public float spreadAngleTotal = 70f;

    [Header("【激光专属】仅fireType=Laser生效")]
    public float laserLength;
    public float laserWidth;
}

/// <summary>武器发射类型，枚举统一管理</summary>
public enum WeaponFireType
{
    Rifle,      //普通步枪子弹
    Shotgun,    //散弹扇形
    Gatling,    //加特林
    LaserBeam,  //激光光束（方块拉伸光束）
    Pistol      //手枪
}
