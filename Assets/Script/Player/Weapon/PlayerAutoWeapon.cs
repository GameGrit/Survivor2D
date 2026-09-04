using UnityEngine;

namespace Player
{
    /// <summary>
    /// 玩家自动武器控制器 —— 策略模式调度器
    ///
    /// 【职责】
    ///   - 只负责计时，到攻击间隔就调用当前武器发射策略的 Fire()
    ///   - 不关心具体怎么发射（单发 / 散弹 / 激光），那是 WeaponFireBase 子类的事
    ///   - 切武器时从新武器物体上获取对应的发射策略组件，并注入数据
    ///
    /// 【数据流向】
    ///   WeaponManager.CurrentWeaponConfig → 注入到 WeaponFireBase.Init()
    ///   WeaponManager.CurrentWeaponObj    → GetComponent&lt;WeaponFireBase&gt;() 获取发射策略
    ///
    /// 【使用方式】
    ///   1. 挂在 Player 身上
    ///   2. bulletRootTransform 拖场景里的子弹池根节点
    ///   3. 每把武器预制体上挂对应的发射脚本（PistolFire / ShotgunFire 等）
    ///   4. WeaponManager 切武器后，调用 RefreshWeaponParams() 刷新
    /// </summary>
    public class PlayerAutoWeapon : MonoBehaviour
    {
        [Header("子弹根节点（场景里的 PoolRoot/BulletRoot）")]
        public Transform bulletRootTransform;

        // 当前武器的发射策略（运行时从武器物体获取）
        private WeaponFireBase _currentFire;

        // 当前攻击间隔（从 WeaponConfig.fireInterval 读）
        public float _attackCd;
        private float _attackTimer;

        private PlayerController _playerController;

        private void Awake()
        {
            if (bulletRootTransform == null)
            {
                Debug.LogError("请把场景 PoolRoot/BulletRoot 拖到 bulletRootTransform！", this);
                enabled = false;
                return;
            }

            _playerController = GetComponent<PlayerController>();
            if (_playerController == null)
                _playerController = GetComponentInParent<PlayerController>();
            if (_playerController == null)
            {
                Debug.LogError("找不到 PlayerController 组件！", this);
                enabled = false;
            }
        }

        void Start()
        {
            // WeaponManager.Start() 会先初始化默认武器，这里刷新发射策略
            RefreshWeaponParams();
        }

        /// <summary>
        /// 刷新当前武器的发射策略和参数
        /// 切武器后必须调用一次，否则还在用旧武器的发射逻辑
        /// </summary>
        public void RefreshWeaponParams()
        {
            WeaponConfig cfg = WeaponManager.Instance.CurrentWeaponConfig;
            if (cfg == null)
            {
                // WeaponManager 可能还没初始化（时序问题），Update中的懒初始化会自动重试
                // 不设为Error，因为这是正常的启动时序，下一帧就会恢复
                Debug.LogWarning("PlayerAutoWeapon: WeaponManager 尚未初始化武器，将在下一帧自动重试...");
                _currentFire = null;
                return;
            }

            // 攻击间隔 = 武器基础间隔（后续可乘玩家攻速加成）
            _attackCd = cfg.fireInterval * PlayerExp.Instance.attackSpeedMultiplier;

            _attackTimer = 0f;

            // 从当前武器物体上获取发射策略组件
            GameObject weaponObj = WeaponManager.Instance.CurrentWeaponObj;
            if (weaponObj != null)
            {
                _currentFire = weaponObj.GetComponent<WeaponFireBase>();
                if (_currentFire == null)
                    _currentFire = weaponObj.GetComponentInChildren<WeaponFireBase>();
            }
            else
            {
                _currentFire = null;
            }

            if (_currentFire == null)
            {
                Debug.LogError(
                    $"PlayerAutoWeapon: 武器「{cfg.weaponName}」的预制体上没有挂 WeaponFireBase 子类！\n" +
                    $"请在武器预制体上挂对应的发射脚本（PistolFire / ShotgunFire / GatlingFire / LaserFire / RifleFire）",
                    this);
                return;
            }

            // 注入武器配置和子弹根节点
            _currentFire.Init(cfg, bulletRootTransform);

            Debug.Log($"[PlayerAutoWeapon] 已切换武器：{cfg.weaponName}，发射策略：{_currentFire.GetType().Name}，" +
                      $"间隔：{_attackCd}s，子弹预制体：{(cfg.bulletPrefab != null ? cfg.bulletPrefab.name : "空！")}，" +
                      $"射击音效：{cfg.fireSfx}");
        }
        /// <summary>
        /// 只刷新攻击间隔（攻速卡片生效时调用，不重新初始化发射策略，避免副作用）
        /// 公式：实际间隔 = 武器基础间隔 × 玩家攻速系数
        /// </summary>
        public void RefreshAttackInterval()
        {
            WeaponConfig cfg = WeaponManager.Instance.CurrentWeaponConfig;
            if (cfg != null)
            {
                _attackCd = cfg.fireInterval * PlayerExp.Instance.attackSpeedMultiplier;
                Debug.Log($"[PlayerAutoWeapon] 攻击间隔已刷新：武器基础={cfg.fireInterval}s × 攻速系数={PlayerExp.Instance.attackSpeedMultiplier:F2} = {_attackCd:F3}s");
            }
        }

        private void Update()
        {
            // 全局暂停
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
                return;

            // 懒初始化：防止 Start 时序问题（PlayerAutoWeapon.Start 比 WeaponManager.Start 先执行）
            // 如果还没拿到发射策略，每帧尝试一次，拿到为止
            if (_currentFire == null)
            {
                RefreshWeaponParams();
                if (_currentFire == null) return;
            }

            _attackTimer += Time.deltaTime;
            if (_attackTimer >= _attackCd)
            {
                _currentFire.Fire();
                _attackTimer = 0f;
            }
        }
    }
}
