using UnityEngine;

/// <summary>
/// 怪物生成器 —— 支持配置驱动的多怪物随机生成
/// 
/// 【生成逻辑】
///   1. 每间隔 baseSpawnInterval 生成一波（间隔随时间难度缩短）
///   2. 每波生成 baseSpawnCount × 难度系数 只怪物
///   3. 每只怪物从 WaveSystem 当前层的怪物列表中按权重随机选取
///   4. 数值缩放 = 时间难度 × 层数难度 × 单怪物配置倍率
/// 
/// 【兼容模式】
///   WaveSystem 没有配置时，全部用 monsterPrefab 生成（旧行为）
/// </summary>
public class MonsterSpawner : BaseMonoSingleton<MonsterSpawner>
{

    [Header("刷怪基础参数")]
    [Header("Addressables 路径名")]
    public string monsterPrefabAddress = "Monster";
    private GameObject _monsterPrefab;
    public GameObject monsterPrefab => _monsterPrefab;


    [Tooltip("每波基础生成数量")]
    public int baseSpawnCount = 3;

    [Tooltip("基础生成间隔（秒），随时间难度缩短")]
    public float baseSpawnInterval = 1.5f;

    [Tooltip("每多少秒难度提升一档")]
    public float difficultyStepTime = 30f;

    [Tooltip("生成间隔下限（防止刷太快）")]
    public float minSpawnInterval = 0.5f;

    [Tooltip("同屏最大怪物数（企业标准：2D割草建议40~80）")]
    public int maxAliveMonster = 60;

    [Header("屏幕外生成偏移（单位：世界坐标）")]
    [Tooltip("怪物在屏幕外多远生成，越小离屏幕越近")]
    public float spawnOutsideOffset = 2f;


    private float _difficultyFactor = 1f;
    private float _spawnTimer;
    private int _aliveMonsterCount;
    private Transform _playerTr;
    private Camera _mainCam;

    protected override void Awake()
    {
        base.Awake();
        _monsterPrefab = AddressablesManager.Instance.LoadAssetSync<GameObject>(monsterPrefabAddress);
        if (_monsterPrefab == null)
        {
            Debug.LogWarning("[MonsterSpawner] 兼容模式怪物预制体加载失败，仅使用 WaveSystem 配置模式");
        }
        // 获取主相机
        _mainCam = Camera.main;
        if (_mainCam == null)
        {
            Debug.LogError("❌ 找不到主相机！请把相机 Tag 设置为 MainCamera");
        }

        // 找玩家
        GameObject playerGo = GameObject.FindWithTag("Player");
        if (playerGo != null)
        {
            _playerTr = playerGo.transform;
            Debug.Log($"✅ 找到玩家：{playerGo.name}，位置：{playerGo.transform.position}");
        }
        else
        {
            Debug.LogError("❌ 找不到 Tag 为 Player 的物体！怪物不会追人！");
        }

        // 监听怪物死亡动画完成事件，负责回对象池
        EventBus.Instance.Subscribe<MonsterDeathAnimationFinishedEventArgs>(OnMonsterDeathAnimationFinished);
    }



    private void Update()
    {
        if (_mainCam == null || _playerTr == null)
            return;

        // 检查是否有可用的怪物来源（配置模式或兼容模式）
        bool hasConfigMonster = WaveSystem.Instance != null && WaveSystem.Instance.HasConfiguredMonsters();
        if (!hasConfigMonster && monsterPrefab == null)
            return;

        // 缓冲期不刷怪（玩家清完一层后的喘息时间）
        if (WaveSystem.Instance != null && WaveSystem.Instance.IsInBreak())
            return;

        // 这层已经刷完所有怪了，停止刷（等清完进缓冲）
        if (WaveSystem.Instance != null && WaveSystem.Instance.IsWaveSpawnComplete())
            return;

        _spawnTimer += Time.deltaTime;
        float realInterval = Mathf.Max(minSpawnInterval, baseSpawnInterval / _difficultyFactor);

        if (_spawnTimer >= realInterval)
        {
            _spawnTimer = 0;
            SpawnWave();
        }
    }

    /// <summary>
    /// 获取当前层允许的最大存活怪物数
    /// 优先用WaveSystem每层配置，没有则用全局maxAliveMonster
    /// </summary>
    private int GetCurrentMaxAlive()
    {
        if (WaveSystem.Instance != null)
        {
            int overrideVal = WaveSystem.Instance.GetMaxAliveOverride();
            if (overrideVal > 0) return overrideVal;
        }
        return maxAliveMonster;
    }

    /// <summary>
    /// 获取屏幕边缘外的随机生成位置
    /// 怪物就在屏幕外一点点生成，一出来就往屏幕里跑
    /// </summary>
    private Vector2 GetRandomSpawnPos()
    {
        // 计算玩家位置为中心的视口边界（怪物跟着玩家周围刷，玩家走到哪怪刷到哪）
        Vector2 playerPos = _playerTr.position;

        // 计算相机视口的半宽半高（世界坐标单位）
        float camHalfHeight = _mainCam.orthographicSize;
        float camHalfWidth = camHalfHeight * _mainCam.aspect;

        // 加上屏幕外偏移
        float halfWidth = camHalfWidth + spawnOutsideOffset;
        float halfHeight = camHalfHeight + spawnOutsideOffset;

        // 四条边随机选一条
        int side = Random.Range(0, 4);
        Vector2 pos = Vector2.zero;

        switch (side)
        {
            case 0: // 左边
                pos.x = playerPos.x - halfWidth;
                pos.y = playerPos.y + Random.Range(-halfHeight, halfHeight);
                break;
            case 1: // 右边
                pos.x = playerPos.x + halfWidth;
                pos.y = playerPos.y + Random.Range(-halfHeight, halfHeight);
                break;
            case 2: // 上边
                pos.x = playerPos.x + Random.Range(-halfWidth, halfWidth);
                pos.y = playerPos.y + halfHeight;
                break;
            case 3: // 下边
                pos.x = playerPos.x + Random.Range(-halfWidth, halfWidth);
                pos.y = playerPos.y - halfHeight;
                break;
        }

        return pos;
    }

    private void SpawnWave()
    {
        int currentMax = GetCurrentMaxAlive();
        if (_aliveMonsterCount >= currentMax)
            return;

        int spawnNum = Mathf.RoundToInt(baseSpawnCount * _difficultyFactor);

        // 这层还剩多少只没刷，单次不超过剩余数
        int remainingInWave = int.MaxValue;
        if (WaveSystem.Instance != null)
        {
            int total = WaveSystem.Instance.GetTotalMonstersInWave();
            int spawned = WaveSystem.Instance.GetSpawnedInWave();
            remainingInWave = Mathf.Max(0, total - spawned);
        }
        spawnNum = Mathf.Min(spawnNum, remainingInWave);

        for (int i = 0; i < spawnNum; i++)
        {
            if (_aliveMonsterCount >= currentMax) break;

            Vector2 spawnPos = GetRandomSpawnPos();

            // 【核心】从当前层配置中按权重随机选一只怪物
            GameObject prefabToSpawn = monsterPrefab; // 默认兼容模式
            float entryHpMult = 1f;
            float entrySpeedMult = 1f;

            WaveMonsterEntry entry = WaveSystem.Instance?.PickRandomMonsterEntry();
            if (entry != null && entry.monsterPrefab != null)
            {
                prefabToSpawn = entry.monsterPrefab;
                entryHpMult = entry.hpMultiplier;
                entrySpeedMult = entry.speedMultiplier;
            }

            if (prefabToSpawn == null)
            {
                Debug.LogWarning("⚠️ MonsterSpawner: 没有可用的怪物预制体，跳过本次生成");
                continue;
            }

            GameObject go = PoolManager.Instance.Get(prefabToSpawn);
            if (go == null) continue;

            go.transform.position = spawnPos;
            go.transform.rotation = Quaternion.identity;

            MonsterBase monster = go.GetComponent<MonsterBase>();
            if (monster != null)
            {
                // 【关键】记录来源预制体，回收时才能找到对应的对象池
                monster.Prefab = prefabToSpawn;

                // 切场景后 _playerTr 可能指向已销毁的旧玩家，这里校验并重新查找
                EnsurePlayerReference();

                monster.Init(_playerTr);

                // 数值缩放 = 时间难度 × 层数难度 × 单怪物配置倍率
                float waveHpFactor = WaveSystem.Instance != null ? WaveSystem.Instance.GetDifficultyFactor() : 1f;
                float waveSpeedFactor = WaveSystem.Instance != null ? WaveSystem.Instance.GetSpeedFactor() : 1f;
                float waveDamageFactor = WaveSystem.Instance != null ? WaveSystem.Instance.GetDamageFactor() : 1f;

                float finalHpFactor = waveHpFactor * entryHpMult;
                float finalSpeedFactor = waveSpeedFactor * entrySpeedMult;

                monster.SetDifficulty(finalHpFactor, finalSpeedFactor, waveDamageFactor);
                monster.OnMonsterDie += OnMonsterDeadCallback;
                _aliveMonsterCount++;

                // 通知层数系统：这层又刷了一只怪
                WaveSystem.Instance?.OnMonsterSpawned();
            }
        }
    }

    /// <summary>怪物死亡回调</summary>
    private void OnMonsterDeadCallback(MonsterBase monster)
    {
        monster.OnMonsterDie -= OnMonsterDeadCallback;
        _aliveMonsterCount--;
    }

    /// <summary>一局重置 —— 切场景后必须重新获取相机和玩家（旧引用已失效）</summary>
    public void ResetSpawner()
    {

        _difficultyFactor = 1f;
        _spawnTimer = 0;
        _aliveMonsterCount = 0;

        // 【关键修复】切场景后旧相机已销毁，必须重新获取
        // 否则 Update() 里 _mainCam==null 直接 return，怪物永远不生成
        _mainCam = Camera.main;
        if (_mainCam == null)
        {
            Debug.LogError("[MonsterSpawner] ResetSpawner 找不到主相机！请把相机 Tag 设置为 MainCamera");
        }

        // 切场景后重新查找玩家引用（DontDestroyOnLoad的单例不会自动刷新）
        EnsurePlayerReference();
    }

    /// <summary>
    /// 确保玩家引用有效，丢失了就重新查找
    /// 【为什么需要】MonsterSpawner是DontDestroyOnLoad单例，切场景后旧玩家被销毁，
    /// _playerTr变成null或指向已销毁对象，生成怪物时传null导致怪物不追人、距离=float.MaxValue
    /// </summary>
    private void EnsurePlayerReference()
    {
        // 三重检查：不仅检查null，还要检查玩家对象是否还活跃
        // 切场景后旧玩家被Destroy，但Unity的==null有时序延迟，必须加activeInHierarchy检查
        if (_playerTr != null
            && _playerTr.gameObject != null
            && _playerTr.gameObject.activeInHierarchy)
        {
            return; // 引用有效，不用重新找
        }

        // 引用无效，重新查找
        GameObject playerGo = GameObject.FindWithTag("Player");
        if (playerGo != null)
        {
            _playerTr = playerGo.transform;
            Debug.Log($"[MonsterSpawner] 重新找到玩家：{playerGo.name}");
        }
        else
        {
            Debug.LogError("[MonsterSpawner] 找不到 Tag=Player 的物体！请检查玩家对象的Tag是否设置为Player，以及玩家是否在场景中");
        }
    }

    /// <summary>
    /// 怪物死亡动画播完回调 —— 回对象池
    /// 【解耦】Spawner只负责回收，不管死亡逻辑和动画
    /// </summary>
    private void OnMonsterDeathAnimationFinished(MonsterDeathAnimationFinishedEventArgs e)
    {
        if (e.monster != null)
        {
            e.monster.RecycleToPool();
        }
    }

    private void OnDestroy()
    {
        EventBus.Instance.Unsubscribe<MonsterDeathAnimationFinishedEventArgs>(OnMonsterDeathAnimationFinished);
    }
}
