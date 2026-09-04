using Config;
using UnityEngine;

namespace Exep
{
    /// <summary>
    /// 经验球内部状态
    /// </summary>
    public enum ExpOrbState
    {
        Falling,    // 刚掉落，向外弹射
        Idle,       // 弹射结束，原地等待玩家靠近
        Attracting  // 进入吸附范围，向玩家飞去
    }


    /// <summary>
    /// 经验球实体 —— 掉落、弹射、待机、自动吸附、拾取
    /// 实现 IPoolable，通过 PoolManager 复用
    /// 
    /// 状态流转：
    ///   OnSpawn → Falling（随机方向弹射）→ 时间到 → Idle
    ///   Idle 中每帧检测玩家距离 → 进入 attractRadius → Attracting
    ///   Attracting 中加速飞向玩家 → 距离 < pickUpDistance → 拾取回收
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class ExpOrb : MonoBehaviour, IPoolable
    {
        public int CoinValue;
        [Header("运行时引用（由 ExpOrbManager 注入）")]
        public ExpOrbConfig config;
        public GameObject prefabRef;  // 对象池回收用的预制体引用

        // 运行时数据
        public int ExpValue { get; private set; }
        public ExpOrbGrade Grade { get; private set; }
        public ExpOrbState CurrentState { get; private set; }

        private Transform _playerTr;
        private SpriteRenderer _spriteRenderer;
        private CircleCollider2D _collider;

        // 弹射状态
        private Vector2 _popVelocity;
        private float _popTimer;

        // 吸附状态
        private float _attractCurrentSpeed;

        // 生命周期
        private float _lifeTimer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<CircleCollider2D>();

            // 经验球用触发器，不参与物理碰撞
            if (_collider != null)
            {
                _collider.isTrigger = true;
            }
        }

        /// <summary>
        /// 对象池取出时调用 —— 由 ExpOrbManager 在 Get 之后调用 Init
        /// 注意：ObjectPoolBase.Get() 会自动调用 OnSpawn，但此时还没有 expValue/config
        /// 所以 OnSpawn 只做保底重置，真正初始化在 Init
        /// </summary>
        public void OnSpawn()
        {
            CurrentState = ExpOrbState.Falling;
            _lifeTimer = 0f;
            _popTimer = 0f;
            _attractCurrentSpeed = 0f;
            _popVelocity = Vector2.zero;
        }

        /// <summary>
        /// 对象池回收时调用
        /// </summary>
        public void OnDespawn()
        {
            CurrentState = ExpOrbState.Idle;
            _popVelocity = Vector2.zero;
            _attractCurrentSpeed = 0f;
            _lifeTimer = 0f;
        }

        /// <summary>
        /// 初始化经验球 —— 由 ExpOrbManager 调用
        /// </summary>
        /// <param name="expValue">经验值</param>
        /// <param name="cfg">配置引用</param>
        /// <param name="player">玩家Transform</param>
        /// <param name="prefab">预制体引用（回收用）</param>
        public void Init(int expValue, int coinValue, ExpOrbConfig cfg, Transform player, GameObject prefab)
        {
            ExpValue = Mathf.Max(1, expValue);
            CoinValue = Mathf.Max(0, coinValue);  
            config = cfg;
            _playerTr = player;
            prefabRef = prefab;

            // 计算等级并应用视觉
            Grade = config.GetGrade(ExpValue);
            ApplyVisual();

            // 随机弹射方向
            float angle = Random.Range(0f, Mathf.PI * 2f);
            _popVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * config.popSpeed;
            _popTimer = 0f;

            CurrentState = ExpOrbState.Falling;
        }

        /// <summary>
        /// 应用视觉表现（颜色 + 缩放）
        /// </summary>
        private void ApplyVisual()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = config.GetColor(Grade);
            }
            transform.localScale = Vector3.one * config.GetScale(Grade);
        }

        private void Update()
        {
            if (config == null || _playerTr == null) return;

            // 生命周期计时
            _lifeTimer += Time.deltaTime;
            if (_lifeTimer >= config.maxLifeTime)
            {
                RecycleSelf();
                return;
            }

            switch (CurrentState)
            {
                case ExpOrbState.Falling:
                    UpdateFalling();
                    break;
                case ExpOrbState.Idle:
                    UpdateIdle();
                    break;
                case ExpOrbState.Attracting:
                    UpdateAttracting();
                    break;
            }
        }

        /// <summary>
        /// 弹射状态：向外飞，速度衰减，时间到转Idle
        /// </summary>
        private void UpdateFalling()
        {
            _popTimer += Time.deltaTime;

            // 速度衰减（指数衰减，手感更自然）
            _popVelocity *= Mathf.Exp(-config.popDrag * Time.deltaTime);

            transform.position += (Vector3)(_popVelocity * Time.deltaTime);

            if (_popTimer >= config.popDuration)
            {
                CurrentState = ExpOrbState.Idle;
                _popVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// 待机状态：检测玩家是否进入吸附范围
        /// 【修复】吸附范围 = 配置基础范围 + 玩家升级卡片获得的拾取范围加成
        /// </summary>
        private void UpdateIdle()
        {
            float dist = Vector2.Distance(transform.position, _playerTr.position);
            // 拾取范围 = 配置基础范围 + 玩家升级卡片获得的拾取范围加成
            float totalRange = config.attractRadius + PlayerExp.Instance.pickupRange;
            if (dist <= totalRange)
            {
                CurrentState = ExpOrbState.Attracting;
                _attractCurrentSpeed = config.attractStartSpeed;
            }
        }

        /// <summary>
        /// 吸附状态：加速飞向玩家，到达后拾取
        /// </summary>
        private void UpdateAttracting()
        {
            Vector2 toPlayer = (Vector2)_playerTr.position - (Vector2)transform.position;
            float dist = toPlayer.magnitude;

            // 到达拾取距离
            if (dist <= config.pickUpDistance)
            {
                PickUp();
                return;
            }

            // 加速（越靠近越快，形成磁吸感）
            _attractCurrentSpeed += config.attractAcceleration * Time.deltaTime;
            _attractCurrentSpeed = Mathf.Min(_attractCurrentSpeed, config.attractMaxSpeed);

            Vector2 dir = toPlayer.normalized;
            transform.position += (Vector3)(dir * _attractCurrentSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 被玩家拾取
        /// </summary>
        private void PickUp()
        {


            // 加金币（如果这个球带金币）
            if (CoinValue > 0)
            {
                CoinManager.Instance.AddCoin(CoinValue);
            }
            // 加经验
            ExpSystem.Instance.AddExp(ExpValue);

            // 发布拾取事件（音效/特效可监听）
            EventBus.Instance.Publish(new ExpOrbPickedEventArgs()
            {
                expValue = ExpValue,
                grade = Grade,
                position = transform.position
            });

            RecycleSelf();
        }

        /// <summary>
        /// 回收到对象池
        /// </summary>
        private void RecycleSelf()
        {
            if (prefabRef != null)
            {
                PoolManager.Instance.Recycle(prefabRef, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
