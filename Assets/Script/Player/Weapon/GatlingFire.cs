using UnityEngine;

namespace Player
{
    /// <summary>
    /// 加特林发射策略：高射速 + 每发轻微随机散射
    ///
    /// 【适用武器】fireType = Gatling
    /// 【特点】
    ///   - 核心是高射速（WeaponConfig.fireInterval 配很小，如 0.05）
    ///   - 每发子弹在基础方向上叠加小随机角度，模拟弹道散布
    ///
    /// 【预热机制说明】
    ///   真正的加特林需要"转速逐渐加快"的预热阶段，这要求 PlayerAutoWeapon
    ///   的攻击间隔随时间变化。当前版本先做基础高射速版，预热可后续扩展：
    ///   在本类加一个 _heatProgress 字段，PlayerAutoWeapon 改为询问当前武器
    ///   的 GetCurrentInterval() 而非读固定 _attackCd。
    /// </summary>
    public class GatlingFire : WeaponFireBase
    {
        [Tooltip("每发子弹的随机散射角度（度），值越大弹道越散")]
        public float randomSpreadAngle = 5f;

        public override void Fire()
        {
            Vector2 basePos = GetFirePosition();
            Vector2 baseDir = GetFireDirection();
            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

            // 叠加随机散射
            float randomOffset = Random.Range(-randomSpreadAngle, randomSpreadAngle);
            float finalAngle = baseAngle + randomOffset;
            float rad = finalAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            SpawnBullet(basePos, dir);
            PublishFireEvent(SfxType.PlayerShoot);
        }
    }
}
