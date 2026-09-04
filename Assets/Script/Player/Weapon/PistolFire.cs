using UnityEngine;

namespace Player
{
    /// <summary>
    /// 手枪发射策略：单发一颗子弹
    ///
    /// 【适用武器】fireType = Pistol
    /// 【特点】每次 Fire() 发射一颗子弹，伤害和射速由 WeaponConfig 决定
    /// </summary>
    public class PistolFire : WeaponFireBase
    {
        public override void Fire()
        {
            Vector2 pos = GetFirePosition();
            Vector2 dir = GetFireDirection();
            SpawnBullet(pos, dir);
            PublishFireEvent(SfxType.PlayerShoot);
        }
    }
}
