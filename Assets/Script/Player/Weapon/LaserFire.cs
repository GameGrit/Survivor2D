using UnityEngine;

namespace Player
{
    /// <summary>
    /// 激光发射策略：瞬时光束 + 穿透所有敌人
    ///
    /// 【和手枪/步枪的本质区别】
    ///   - 手枪/步枪走 SpawnBullet()，生成 Bullet 对象飞出去，碰到一个敌人就消失
    ///   - 激光不走 Bullet！用 Physics2D.RaycastAll 沿射线检测所有敌人，逐一造成伤害（穿透）
    ///   - 视觉上生成一个 LaserBeam 光束贴图，拉伸覆盖从枪口到最大射程，持续0.1秒后消失
    ///
    /// 【伤害计算】
    ///   伤害 = 玩家基础攻击力(PlayerExp.attackDamage) + 武器伤害加成(WeaponConfig.damage)
    ///   和 Bullet 的伤害公式一致，但不会被 Bullet.OnSpawn 里的默认值覆盖（因为根本不用Bullet）
    ///
    /// 【配置依赖（WeaponConfig）】
    ///   - bulletPrefab  → 拖光束贴图预制体（必须挂 LaserBeam 组件，不是 Bullet！）
    ///   - laserLength   → 激光最大射程（世界单位），<=0 时用默认15
    ///   - laserWidth    → 光束宽度（世界单位），>0 时覆盖 LaserBeam.beamWidth
    ///   - damage        → 武器伤害加成
    ///   - fireInterval  → 发射间隔（激光连射速度）
    ///   - fireSfx       → 射击音效
    ///
    /// 【编辑器里要做的事】
    ///   1. 新建一个空预制体，挂 SpriteRenderer + LaserBeam，拖一张光束长条贴图
    ///   2. 把这个预制体拖到激光武器的 WeaponConfig.bulletPrefab 上
    ///   3. 激光武器预制体上挂本脚本 LaserFire（不是 PistolFire！）
    /// </summary>
    public class LaserFire : WeaponFireBase
    {
        [Header("激光调试")]
        [Tooltip("在Scene视图里画出激光射线（红色），方便调试射程和方向")]
        public bool drawDebugRay = false;

        [Tooltip("激光默认最大射程（laserLength<=0时用这个值）")]
        public float defaultLaserLength = 15f;

        public override void Fire()
        {
            Vector2 firePos = GetFirePosition();
            Vector2 fireDir = GetFireDirection().normalized;

            // 1. 确定最大射程
            float maxLength = (Config != null && Config.laserLength > 0f)
                ? Config.laserLength
                : defaultLaserLength;

            // 2. 射线检测：找出射线上所有碰撞体，过滤出Enemy，逐一造成伤害（穿透）
            //    RaycastAll 返回按距离排序的所有命中点，包含被前面物体挡住的后面的敌人
            RaycastHit2D[] hits = Physics2D.RaycastAll(firePos, fireDir, maxLength);

            int damage = PlayerExp.Instance.attackDamage + Mathf.RoundToInt(Config != null ? Config.damage : 0f);

            // 光束终点：默认射到最大射程；如果命中了非敌人的障碍物（如墙），终点在障碍物处
            // 目前项目没有墙，先直接射到最大距离；后续加障碍物时可在这里检测非Enemy碰撞体
            Vector2 endPos = firePos + fireDir * maxLength;

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null) continue;

                // 只处理Enemy标签的碰撞体（穿透所有敌人，不被任何一个挡住）
                if (!hit.collider.CompareTag("Enemy")) continue;

                // 兼容HurtBox挂在子对象上的情况（企业标准做法）
                MonsterBase monster = hit.collider.GetComponentInParent<MonsterBase>();
                if (monster != null)
                {
                    monster.TakeDamage(damage);

                    // 发布命中事件（命中特效/震屏等订阅）
                    EventBus.Instance.Publish(new BulletHitEventArgs
                    {
                        hitPosition = hit.point,
                        damage = damage,
                        monster = monster
                    });
                }
            }

            // 3. 调试用：Scene视图画红线
            if (drawDebugRay)
            {
                Debug.DrawRay(firePos, fireDir * maxLength, Color.red, 0.2f);
            }

            // 4. 生成光束贴图（从对象池取，用 WeaponConfig.bulletPrefab 作为光束预制体）
            SpawnBeam(firePos, endPos);

            // 5. 射击音效（从 WeaponConfig.fireSfx 读，无参版本自动用配置）
            PublishFireEvent();
        }

        /// <summary>
        /// 从对象池取光束，设置形态，注册回收回调
        /// </summary>
        private void SpawnBeam(Vector2 start, Vector2 end)
        {
            if (Config == null || Config.bulletPrefab == null)
            {
                Debug.LogWarning("[LaserFire] WeaponConfig.bulletPrefab 为空！激光只有伤害没有视觉效果。" +
                                 "请把光束预制体（挂LaserBeam）拖到bulletPrefab上。", this);
                return;
            }

            // 从对象池取光束（预加载5个，激光持续短、复用率高）
            GameObject beamGo = PoolManager.Instance.Get(Config.bulletPrefab, BulletRoot, 5);
            if (beamGo == null)
            {
                Debug.LogError("[LaserFire] 从对象池获取光束预制体失败！", this);
                return;
            }

            LaserBeam beam = beamGo.GetComponent<LaserBeam>();
            if (beam == null)
            {
                Debug.LogError($"[LaserFire] 预制体 {Config.bulletPrefab.name} 上没有挂 LaserBeam 组件！" +
                               $"激光的bulletPrefab应该是光束贴图预制体，不是子弹预制体。", this);
                PoolManager.Instance.Recycle(Config.bulletPrefab, beamGo);
                return;
            }

            // 用 WeaponConfig.laserWidth 覆盖光束宽度（如果配了的话）
            if (Config.laserWidth > 0f)
            {
                beam.beamWidth = Config.laserWidth;
            }

            // 设置光束形态（位置/旋转/缩放）
            beam.SetBeam(start, end);

            // 注册回收回调（先减后加，防止重复注册）
            beam.OnNeedRecycle -= OnBeamRecycle;
            beam.OnNeedRecycle += OnBeamRecycle;
        }

        /// <summary>
        /// 光束回收回调：从对象池回收
        /// </summary>
        private void OnBeamRecycle(GameObject beamObj)
        {
            if (beamObj == null || Config == null) return;

            LaserBeam beam = beamObj.GetComponent<LaserBeam>();
            if (beam != null)
                beam.OnNeedRecycle -= OnBeamRecycle;

            PoolManager.Instance.Recycle(Config.bulletPrefab, beamObj);
        }
    }
}
