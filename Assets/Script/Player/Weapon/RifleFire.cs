using UnityEngine;

namespace Player
{
    /// <summary>
    /// 步枪发射策略：单发一颗子弹
    ///
    /// 【适用武器】fireType = Rifle
    /// 【特点】和手枪同为单发，但通常 fireInterval 更小（射速更快）、bulletSpeed 更高
    /// 【可扩展】如需三连发点射，可在此类加一个 burstCount 字段循环发射
    /// </summary>
    public class RifleFire : WeaponFireBase
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
