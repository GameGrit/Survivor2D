using UnityEngine;

namespace Player
{
    /// <summary>
    /// 武器发射策略抽象基类 —— 策略模式
    ///
    /// 【设计思路】
    ///   - 每把武器预制体上挂一个具体发射脚本（PistolFire / ShotgunFire / GatlingFire 等）
    ///   - PlayerAutoWeapon 只负责计时，到点调用 Fire()，不关心具体发射模式
    ///   - 公共逻辑（从对象池取子弹、设方向、注册回收）封装在基类
    ///   - 子类只实现差异化的 Fire() 发射模式
    ///
    /// 【数据注入流程】
    ///   PlayerAutoWeapon.RefreshWeaponParams()
    ///     → 从 WeaponManager.CurrentWeaponObj 上 GetComponent&lt;WeaponFireBase&gt;()
    ///     → 调用 Init(weaponConfig, bulletRootTransform) 注入武器配置和子弹根节点
    /// </summary>
    public abstract class WeaponFireBase : MonoBehaviour
    {
        // ===== 运行时注入的数据（不要在 Inspector 填）=====
        protected WeaponConfig Config { get; private set; }
        protected Transform BulletRoot { get; private set; }

        // ===== 组件引用 =====
        protected WeaponDir _weaponDir;
        protected PlayerController _playerController;

        /// <summary>
        /// 初始化：由 PlayerAutoWeapon 在切武器时调用，注入武器配置和子弹根节点
        /// </summary>
        public virtual void Init(WeaponConfig config, Transform bulletRoot)
        {
            Config = config;
            BulletRoot = bulletRoot;

            // 武器方向组件（挂在同一物体上）
            _weaponDir = GetComponent<WeaponDir>();
            // 切武器后立即刷新一次朝向，避免第一帧 rotation=identity 导致 FirePoint 位置错乱
            if (_weaponDir != null) _weaponDir.RefreshAim();

            // 玩家控制器（在父物体上）
            _playerController = GetComponentInParent<PlayerController>();
        }

        /// <summary>
        /// 发射！子类实现具体发射模式（单发 / 散弹 / 激光等）
        /// </summary>
        public abstract void Fire();

        // ============================================================
        //  以下是子类可复用的公共工具方法
        // ============================================================

        /// <summary>
        /// 从对象池取一颗子弹，设置位置、方向、旋转，并注册回收回调
        /// </summary>
        /// <param name="position">生成位置（世界坐标）</param>
        /// <param name="direction">飞行方向（内部会归一化）</param>
        /// <returns>子弹 GameObject，子类可继续修改属性；失败返回 null</returns>
        protected GameObject SpawnBullet(Vector2 position, Vector2 direction)
        {
            if (Config == null || Config.bulletPrefab == null)
            {
                Debug.LogError($"[{GetType().Name}] WeaponConfig 或 bulletPrefab 为空！", this);
                return null;
            }

            // 从对象池取子弹
            GameObject bulletGo = PoolManager.Instance.Get(Config.bulletPrefab, BulletRoot, 15);
            if (bulletGo == null) return null;

            // 设置位置
            bulletGo.transform.position = position;

            // 设置方向
            Bullet bullet = bulletGo.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.SetDirection(direction.normalized);

                // 【关键】覆盖子弹伤害和速度（在 OnSpawn 之后执行，不会被覆盖）
                // - 伤害 = 玩家基础攻击力 + 武器伤害加成（WeaponConfig.damage）
                // - 速度 = 武器配置的子弹速度（WeaponConfig.bulletSpeed）
                // 注意：Bullet.OnSpawn() 会先从 PlayerExp 读默认值，这里用武器属性覆盖
                bullet.damage = PlayerExp.Instance.attackDamage + Mathf.RoundToInt(Config.damage);
                bullet.moveSpeed = Config.bulletSpeed;
            }

            // 子弹图片朝向飞行方向
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bulletGo.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 注册回收回调
            if (bullet != null)
            {
                bullet.OnNeedRecycle -= OnBulletRecycle;
                bullet.OnNeedRecycle += OnBulletRecycle;
            }

            return bulletGo;
        }

        /// <summary>
        /// 获取枪口世界坐标（优先用 WeaponDir 的 firePoint，其次用武器自身位置）
        /// </summary>
        protected Vector2 GetFirePosition()
        {
            if (_weaponDir != null) return _weaponDir.GetFirePointPosition();
            return transform.position;
        }

        /// <summary>
        /// 获取枪口实际朝向（已考虑 flipX 翻转）
        /// </summary>
        protected Vector2 GetFireDirection()
        {
            if (_weaponDir != null) return _weaponDir.GetFireDirection();
            // 兜底：用玩家移动方向，没有输入就朝右
            if (_playerController != null && _playerController.dir.sqrMagnitude > 0.01f)
                return _playerController.dir.normalized;
            return Vector2.right;
        }

        /// <summary>
        /// 发布射击音效事件（通过 EventBus 解耦，不直接依赖 AudioManager）
        /// 音效类型从 WeaponConfig.fireSfx 读取，不同武器可配不同音效
        /// </summary>
        protected void PublishFireEvent()
        {
            SfxType sfx = Config != null ? Config.fireSfx : SfxType.PlayerShoot;
            EventBus.Instance.Publish(new BulletFiredEventArgs { sfxType = sfx });
        }

        /// <summary>
        /// 兼容旧调用：子类如果还在传具体 SfxType，优先用 Config 里的配置
        /// </summary>
        protected void PublishFireEvent(SfxType sfxType)
        {
            // 优先使用 WeaponConfig 中配置的音效，保证切换武器后音效跟随变化
            SfxType sfx = Config != null ? Config.fireSfx : sfxType;
            EventBus.Instance.Publish(new BulletFiredEventArgs { sfxType = sfx });
        }

        /// <summary>
        /// 子弹回收回调：从对象池回收
        /// </summary>
        private void OnBulletRecycle(GameObject bulletObj)
        {
            if (bulletObj == null || Config == null) return;
            Bullet bullet = bulletObj.GetComponent<Bullet>();
            if (bullet != null)
                bullet.OnNeedRecycle -= OnBulletRecycle;
            PoolManager.Instance.Recycle(Config.bulletPrefab, bulletObj);
        }
    }
}
