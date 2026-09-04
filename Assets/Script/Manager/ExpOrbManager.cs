using Config;
using Exep;
using UnityEngine;

namespace Manager
{
    /// <summary>
    /// 经验球管理器 —— 单例
    /// 职责：
    ///   1. 持有 ExpOrbConfig 配置和 ExpOrb 预制体引用
    ///   2. 缓存玩家 Transform（避免每只怪 Find）
    ///   3. 提供 SpawnExpOrb 接口，怪物死亡时调用
    ///   4. 内部通过 PoolManager 复用经验球对象
    /// 
    /// 使用方式：
    ///   在场景中挂一个 ExpOrbManager，拖入 config 和 prefab
    ///   怪物死亡：ExpOrbManager.Instance.SpawnExpOrb(transform.position, expValue);
    /// </summary>
    public class ExpOrbManager : BaseMonoSingleton<ExpOrbManager>
    {
        [Header("Addressables 路径名")]
        public string orbConfigAddress = "exp_orb_config";
        public string expOrbPrefabAddress = "exp_orb_prefab";

        private ExpOrbConfig _orbConfig;
        private GameObject _expOrbPrefab;

        public ExpOrbConfig orbConfig => _orbConfig;
        public GameObject expOrbPrefab => _expOrbPrefab;


        [Header("对象池")]
        [Tooltip("初始预生成数量")]
        public int initPoolCount = 30;

        [Tooltip("经验球父节点（不填则挂在自己下面）")]
        public Transform orbRoot;

        // 缓存玩家引用
        private Transform _playerTr;
        private bool _playerCached;

        protected override void Awake()
        {
            base.Awake();

            // 从 Addressables 加载配置
            _orbConfig = AddressablesManager.Instance.LoadAssetSync<ExpOrbConfig>(orbConfigAddress);
            if (_orbConfig == null)
            {
                _orbConfig = ScriptableObject.CreateInstance<ExpOrbConfig>();
                Debug.LogWarning("[ExpOrbManager] 配置加载失败，使用默认配置");
            }

            // 从 Addressables 加载预制体
            _expOrbPrefab = AddressablesManager.Instance.LoadAssetSync<GameObject>(expOrbPrefabAddress);
            if (_expOrbPrefab == null)
            {
                Debug.LogError("[ExpOrbManager] 经验球预制体加载失败！检查路径名是否是 exp_orb_prefab");
            }
        }

        private void Start()
        {
            CachePlayer();

            // 预初始化对象池（提前生成一批，避免运行时 Instantiate 卡顿）
            if (expOrbPrefab != null)
            {
                Transform parent = orbRoot != null ? orbRoot : transform;
                // 第一次 Get 会创建池并预生成 initCount 个对象，同时返回一个 active 对象
                GameObject warmupGo = PoolManager.Instance.Get(expOrbPrefab, parent, initPoolCount);
                // 把预热用的这个对象回收掉，池就建好了
                if (warmupGo != null)
                {
                    PoolManager.Instance.Recycle(expOrbPrefab, warmupGo);
                }
                Debug.Log($"[ExpOrbManager] 对象池预热完成，初始数量={initPoolCount}");
            }
            else
            {
                Debug.LogError("[ExpOrbManager] 未拖入 expOrbPrefab！经验球无法生成");
            }
        }

        /// <summary>
        /// 缓存玩家引用（只 Find 一次）
        /// </summary>
        private void CachePlayer()
        {
            GameObject playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null)
            {
                _playerTr = playerGo.transform;
                _playerCached = true;
                Debug.Log($"[ExpOrbManager] 已缓存玩家引用：{playerGo.name}");
            }
            else
            {
                Debug.LogError("[ExpOrbManager] 找不到 Tag=Player 的物体！经验球无法吸附");
                _playerCached = false;
            }
        }

        /// <summary>
        /// 生成一个经验球
        /// </summary>
        /// <param name="position">掉落位置</param>
        /// <param name="expValue">经验值</param>
        public void SpawnExpOrb(Vector3 position, int expValue, int coinValue = 0)
        {
            if (expValue <= 0) return;
            if (expOrbPrefab == null || orbConfig == null) return;

            // 玩家引用失效时重新缓存（比如玩家死亡重生后）
            if (!_playerCached || _playerTr == null)
            {
                CachePlayer();
                if (!_playerCached) return;
            }

            Transform parent = orbRoot != null ? orbRoot : transform;
            GameObject go = PoolManager.Instance.Get(expOrbPrefab, parent, initPoolCount);
            if (go == null)
            {
                Debug.LogError("[ExpOrbManager] PoolManager.Get 返回 null");
                return;
            }

            go.transform.position = position;

            ExpOrb orb = go.GetComponent<ExpOrb>();
            if (orb == null)
            {
                Debug.LogError("[ExpOrbManager] 预制体缺少 ExpOrb 组件！", go);
                Destroy(go);
                return;
            }

            orb.Init(expValue, coinValue, orbConfig, _playerTr, expOrbPrefab);
        }

        /// <summary>
        /// 批量生成经验球（大怪死亡时分裂成多个小球，视觉更爽）
        /// </summary>
        /// <param name="position">掉落中心位置</param>
        /// <param name="totalExp">总经验值</param>
        /// <param name="orbCount">分裂成几个球</param>
        public void SpawnExpOrbs(Vector3 position, int totalExp, int orbCount)
        {
            if (orbCount <= 1)
            {
                SpawnExpOrb(position, totalExp);
                return;
            }

            int perOrb = Mathf.Max(1, totalExp / orbCount);
            int remainder = totalExp - perOrb * orbCount;

            for (int i = 0; i < orbCount; i++)
            {
                int exp = perOrb + (i < remainder ? 1 : 0);
                SpawnExpOrb(position, exp);
            }
        }

        /// <summary>
        /// 重新缓存玩家（玩家重生后调用）
        /// </summary>
        public void RefreshPlayerCache()
        {
            _playerCached = false;
            CachePlayer();
        }
    }
}
