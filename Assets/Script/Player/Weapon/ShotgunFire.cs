using UnityEngine;

namespace Player
{
    /// <summary>
    /// 散弹发射策略：扇形发射多颗子弹
    ///
    /// 【适用武器】fireType = Shotgun
    /// 【参数来源】从 WeaponConfig 读取：
    ///   - pelletCount     弹丸数量（默认6）
    ///   - spreadAngleTotal 总散射角度（默认70度）
    ///
    /// 【发射逻辑】
    ///   以基础方向为中心，左右各 spreadAngleTotal/2 的范围内
    ///   均匀分布 pelletCount 颗子弹，每颗角度间隔 = total/(count-1)
    /// </summary>
    public class ShotgunFire : WeaponFireBase
    {
        public override void Fire()
        {
            if (Config == null) return;

            Vector2 basePos = GetFirePosition();
            Vector2 baseDir = GetFireDirection();
            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

            // 从武器配置读散弹参数
            int count = Mathf.Max(1, Config.pelletCount);
            float totalSpread = Config.spreadAngleTotal;

            // 每颗子弹之间的角度间隔
            float stepAngle = count > 1 ? totalSpread / (count - 1) : 0f;
            // 起始角度（居中对齐，第一颗在最左侧）
            float startAngle = baseAngle - totalSpread * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float currentAngle = startAngle + stepAngle * i;
                float rad = currentAngle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                SpawnBullet(basePos, dir);
            }

            PublishFireEvent(SfxType.PlayerShoot);
        }
    }
}
