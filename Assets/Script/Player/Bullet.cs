using System;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// 子弹组件 —— 稳定版
    /// 
    /// 【设计说明】
    ///   - 移动用 transform.Translate（简单可靠，不会和物理系统不同步导致卡顿）
    ///   - 碰撞主要靠 OnTriggerEnter2D（怪物有 Rigidbody2D，子弹不需要）
    ///   - 额外加一层射线检测防止高速穿透，但只在速度超过阈值时启用
    ///   - 自动补全 Collider2D 并确保覆盖子弹图片范围
    /// </summary>
    public class Bullet : MonoBehaviour, IPoolable
    {
        [Header("子弹配置")]
        [Tooltip("子弹存活时间（秒）")]
        public float lifeTime = 2f;

        [Tooltip("启用射线连续检测（速度极快时防止穿透，普通速度建议关闭以节省性能）")]
        public bool enableRaycastCheck = false;

        [Tooltip("射线检测距离倍率（相对于每帧位移）")]
        [Range(1f, 2f)] public float raycastDistanceMultiplier = 1.2f;

        [HideInInspector] public int damage;      // 运行时从PlayerExp读
        [HideInInspector] public float moveSpeed; // 运行时从PlayerExp读

        private float _timer;
        private Vector2 _moveDir;
        private Collider2D _collider;

        // 缓存预制体原始rotation和scale，对象池复用时重置回原始值
        // 【为什么必须重置scale】ObjectPoolBase.Get() 中 SetParent(worldPositionStays=true)
        // 会自动修改 localScale 以保持世界缩放不变，若不重置，对象池复用后子弹会越变越大
        private Quaternion _originalRotation;
        private Vector3 _originalScale;
        private bool _originalCached = false;
 
        public Action<GameObject> OnNeedRecycle;

        private void Awake()
        {
            // 缓存预制体原始rotation和scale（只缓存一次，对象池复用后也用这个原始值）
            if (!_originalCached)
            {
                _originalRotation = transform.rotation;
                _originalScale = transform.localScale;
                _originalCached = true;
            }

            // 自动补全 Collider2D（Trigger）
            _collider = GetComponent<Collider2D>();
            if (_collider == null)
            {
                // 默认加一个圆形碰撞体，半径0.2（覆盖常见像素子弹）
                CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
                circle.radius = 0.2f;
                circle.isTrigger = true;
                _collider = circle;
                Debug.LogWarning($"⚠️ 子弹 {gameObject.name} 未挂Collider2D，已自动添加CircleCollider2D(radius=0.2)");
            }
            else if (!_collider.isTrigger)
            {
                _collider.isTrigger = true;
            }
        }

        public void OnSpawn()
        {
            // 从属性中心拿当前攻击力和子弹速度
            damage = PlayerExp.Instance.attackDamage;
            moveSpeed = PlayerExp.Instance.bulletSpeed;
            _timer = 0f;
            _moveDir = Vector2.right;

            // 【关键】重置rotation和scale，防止对象池复用时残留上一颗子弹的朝向和缩放
            // （SetParent(worldPositionStays=true) 会修改 localScale，必须重置否则子弹越变越大）
            transform.rotation = _originalRotation;
            transform.localScale = _originalScale;

            // 确保碰撞器启用
            if (_collider != null)
            {
                _collider.enabled = true;
            }
        }

        public void OnDespawn()
        {
            OnNeedRecycle = null;
            _moveDir = Vector2.zero;
        }

        public void SetDirection(Vector2 dir)
        {
            _moveDir = dir.normalized;
        }

        private void Update()
        {
            // 全局暂停检查：暂停时子弹不移动不计时
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
                return;

            _timer += Time.deltaTime;

            if (_timer >= lifeTime)
            {
                OnNeedRecycle?.Invoke(gameObject);
                return;
            }

            float stepDistance = moveSpeed * Time.deltaTime;
            Vector2 currentPos = transform.position;

            // 可选：高速子弹用射线检测防止穿透（普通速度不启用，避免性能开销）
            if (enableRaycastCheck && moveSpeed > 5f)
            {
                float rayDistance = stepDistance * raycastDistanceMultiplier;
                RaycastHit2D hit = Physics2D.Raycast(currentPos, _moveDir, rayDistance);
                if (hit.collider != null && hit.collider.CompareTag("Enemy"))
                {
                    // 用InParent兼容受击盒挂在子对象上的情况
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
                    OnNeedRecycle?.Invoke(gameObject);
                    return;
                }
            }

            // 用 transform.Translate 移动（稳定，不会和物理系统不同步）
            transform.Translate(_moveDir * stepDistance, Space.World);
        }

        /// <summary>
        /// 主要碰撞检测：Trigger 进入时触发伤害
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Enemy"))
            {
                // 用InParent兼容受击盒挂在子对象上的情况（企业标准HurtBox做法）
                MonsterBase monster = other.GetComponentInParent<MonsterBase>();
                if (monster != null)
                {
                    monster.TakeDamage(damage);
                    // 发布命中事件（命中特效/震屏等订阅）
                    EventBus.Instance.Publish(new BulletHitEventArgs
                    {
                        hitPosition = transform.position,
                        damage = damage,
                        monster = monster
                    });
                }
                OnNeedRecycle?.Invoke(gameObject);
            }
        }
    }
}
