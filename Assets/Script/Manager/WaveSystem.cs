using UnityEngine;

/// <summary>
/// 层数/波次系统 —— 固定数量版
/// 
/// 【核心逻辑】
///   1. 每层固定怪物总数（totalMonsters），分批刷完就停
///   2. 这层所有怪都死完 → 进入缓冲期（breakTime秒）
///   3. 缓冲结束 → 自动升层 → 刷下一层的怪
///   4. 越往后怪越多、越强（血量/速度/伤害倍率递增）
/// 
/// 【与MonsterSpawner协作】
///   - Spawner 每帧问 IsInBreak() 和 IsWaveSpawnComplete()，决定是否继续刷
///   - 每刷一只怪调用 OnMonsterSpawned()
///   - 每死一只怪调用 OnMonsterKilled()
/// </summary>
public class WaveSystem : BaseMonoSingleton<WaveSystem>
{
    //[Header("波次配置（拖入WaveSystemConfig资产）")]
    //[Tooltip("不拖则用下方兼容模式参数")]
    //public WaveSystemConfig waveConfig;
    [Header("Addressables 路径名")]
    public string waveConfigAddress = "wave_config";
    private WaveSystemConfig _waveConfig;
    public WaveSystemConfig waveConfig => _waveConfig;


    [Header("【兼容模式】无配置时使用以下参数")]
    [Tooltip("每层固定怪物数")]
    public int fallbackTotalMonsters = 10;

    [Tooltip("清完后缓冲时间（秒）")]
    public float fallbackBreakTime = 3f;

    [Tooltip("每层怪物血量增长系数")]
    public float hpGrowthPerWave = 0.15f;

    [Tooltip("每层怪物速度增长系数")]
    public float speedGrowthPerWave = 0.05f;

    [Tooltip("每层怪物经验掉落增长系数")]
    public float expGrowthPerWave = 0.1f;

    [Header("怪物伤害递增（兼容模式）")]
    [Tooltip("每层怪物接触伤害增长系数（第1层=1倍，第N层=1+(N-1)*该值）")]
    public float damageGrowthPerWave = 0.2f;

    [Tooltip("怪物接触伤害硬上限（不管层数多高，单次伤害不超过这个值）")]
    public float maxMonsterDamage = 50f;

    //运行时
    public int currentWave { get; private set; } = 1;

    // 这层已生成 / 已死亡计数
    private int _spawnedInWave;
    private int _killedInWave;

    // 缓冲期
    private bool _isInBreak = false;
    private float _breakTimer;
    protected override void Awake()
    {
        base.Awake();
        _waveConfig = AddressablesManager.Instance.LoadAssetSync<WaveSystemConfig>(waveConfigAddress);
        if (_waveConfig == null)
        {
            Debug.LogWarning("[WaveSystem] 波次配置加载失败，使用兼容模式参数");
        }
    }
    private void Update()
    {
        // 缓冲期倒计时
        if (_isInBreak)
        {
            _breakTimer -= Time.deltaTime;
            if (_breakTimer <= 0f)
            {
                NextWave();
            }
        }
    }

    /// <summary>当前层是否处于配置模式</summary>
    public bool IsConfiguredWave
    {
        get
        {
            if (waveConfig == null) return false;
            return waveConfig.GetWaveConfig(currentWave - 1) != null;
        }
    }

    // ==================== 数值倍率 ====================

    public float GetDifficultyFactor()
    {
        var cfg = waveConfig?.GetWaveConfig(currentWave - 1);
        if (cfg != null) return cfg.hpMultiplier;

        if (waveConfig != null)
        {
            var lastCfg = waveConfig.GetLastWaveConfig();
            int extraWaves = currentWave - waveConfig.waves.Count;
            float baseHp = lastCfg != null ? lastCfg.hpMultiplier : 1f;
            return baseHp * (1f + (extraWaves - 1) * waveConfig.infiniteHpGrowth);
        }
        return 1f + (currentWave - 1) * hpGrowthPerWave;
    }

    public float GetSpeedFactor()
    {
        var cfg = waveConfig?.GetWaveConfig(currentWave - 1);
        if (cfg != null) return cfg.speedMultiplier;

        if (waveConfig != null)
        {
            var lastCfg = waveConfig.GetLastWaveConfig();
            int extraWaves = currentWave - waveConfig.waves.Count;
            float baseSpeed = lastCfg != null ? lastCfg.speedMultiplier : 1f;
            return baseSpeed * (1f + (extraWaves - 1) * waveConfig.infiniteSpeedGrowth);
        }
        return 1f + (currentWave - 1) * speedGrowthPerWave;
    }

    public float GetExpFactor()
    {
        var cfg = waveConfig?.GetWaveConfig(currentWave - 1);
        if (cfg != null) return cfg.expMultiplier;

        if (waveConfig != null)
        {
            var lastCfg = waveConfig.GetLastWaveConfig();
            int extraWaves = currentWave - waveConfig.waves.Count;
            float baseExp = lastCfg != null ? lastCfg.expMultiplier : 1f;
            return baseExp * (1f + (extraWaves - 1) * waveConfig.infiniteExpGrowth);
        }
        return 1f + (currentWave - 1) * expGrowthPerWave;
    }

    /// <summary>
    /// 获取当前层怪物伤害倍率
    /// 【设计】伤害随层数线性递增，达到硬上限后不再增长
    /// 配置模式下也用兼容模式的增长曲线（伤害不需要每层精确配置）
    /// </summary>
    public float GetDamageFactor()
    {
        return 1f + (currentWave - 1) * damageGrowthPerWave;
    }

    /// <summary>
    /// 根据基础伤害和当前层数计算实际伤害（自动 Clamp 到上限）
    /// </summary>
    /// <param name="baseDamage">怪物预制体上的基础接触伤害</param>
    /// <returns>实际伤害（不超过 maxMonsterDamage）</returns>
    public float CalculateActualDamage(float baseDamage)
    {
        float raw = baseDamage * GetDamageFactor();
        return Mathf.Min(raw, maxMonsterDamage);
    }

    public int GetMaxAliveOverride()
    {
        var cfg = waveConfig?.GetWaveConfig(currentWave - 1);
        if (cfg != null) return cfg.maxAliveOverride;
        return 0;
    }

    // ==================== 固定数量 + 清层 + 缓冲 ====================

    /// <summary>获取这层固定怪物总数</summary>
    public int GetTotalMonstersInWave()
    {
        var cfg = waveConfig?.GetWaveConfig(currentWave - 1);
        if (cfg != null) return cfg.totalMonsters;
        return fallbackTotalMonsters;
    }

    /// <summary>获取清完这层后的缓冲时间（秒）</summary>
    public float GetBreakTime()
    {
        var cfg = waveConfig?.GetWaveConfig(currentWave - 1);
        if (cfg != null) return cfg.breakTime;
        return fallbackBreakTime;
    }

    /// <summary>这层是否已经刷完所有怪（Spawner查询用，刷完就停）</summary>
    public bool IsWaveSpawnComplete()
    {
        return _spawnedInWave >= GetTotalMonstersInWave();
    }

    /// <summary>是否处于缓冲期（Spawner查询用，缓冲期不刷怪）</summary>
    public bool IsInBreak()
    {
        return _isInBreak;
    }

    /// <summary>缓冲期剩余时间（UI显示用）</summary>
    public float GetBreakTimer()
    {
        return Mathf.Max(0f, _breakTimer);
    }

    /// <summary>这层已生成的怪物数</summary>
    public int GetSpawnedInWave() => _spawnedInWave;

    /// <summary>这层已死亡的怪物数</summary>
    public int GetKilledInWave() => _killedInWave;

    /// <summary>Spawner每刷一只怪调用一次</summary>
    public void OnMonsterSpawned()
    {
        _spawnedInWave++;
    }

    /// <summary>怪物死亡时调用，检查是否清完这层所有怪</summary>
    public void OnMonsterKilled()
    {
        _killedInWave++;

        int total = GetTotalMonstersInWave();

        // 这层所有怪都已生成且全部死亡 → 进入缓冲
        if (_spawnedInWave >= total && _killedInWave >= total && !_isInBreak)
        {
            StartBreak();
        }
    }

    /// <summary>开始缓冲期</summary>
    private void StartBreak()
    {
        _isInBreak = true;
        _breakTimer = GetBreakTime();
        Debug.Log($"🌊 第 {currentWave} 层已清除！缓冲 {GetBreakTime()} 秒后进下一层");
    }

    /// <summary>缓冲结束，进入下一层</summary>
    private void NextWave()
    {
        currentWave++;
        _spawnedInWave = 0;
        _killedInWave = 0;
        _isInBreak = false;

        Debug.Log($"🌊 进入第 {currentWave} 层！血量倍率={GetDifficultyFactor():F2}，速度倍率={GetSpeedFactor():F2}，伤害倍率={GetDamageFactor():F2}，怪物总数={GetTotalMonstersInWave()}");

        // 发布层数变化事件，HUD刷新
        EventBus.Instance.Publish(new WaveChangedEventArgs()
        {
            newWave = currentWave,
            difficultyFactor = GetDifficultyFactor()
        });
    }

    /// <summary>当前层是否有可用的怪物配置</summary>
    public bool HasConfiguredMonsters()
    {
        if (waveConfig == null) return false;

        var cfg = GetEffectiveWaveConfig();
        if (cfg == null || cfg.monsters == null || cfg.monsters.Count == 0) return false;

        foreach (var entry in cfg.monsters)
        {
            if (entry != null && entry.monsterPrefab != null) return true;
        }
        return false;
    }

    /// <summary>获取当前层实际生效的配置（当前层为空时回退到最后一层有怪物的）</summary>
    private WaveLevelConfig GetEffectiveWaveConfig()
    {
        if (waveConfig == null) return null;

        var cfg = waveConfig.GetWaveConfig(currentWave - 1);
        if (cfg != null && HasValidMonsters(cfg)) return cfg;

        for (int i = currentWave - 2; i >= 0; i--)
        {
            var fallback = waveConfig.GetWaveConfig(i);
            if (fallback != null && HasValidMonsters(fallback))
            {
                return fallback;
            }
        }

        return waveConfig.GetLastWaveConfig();
    }

    private bool HasValidMonsters(WaveLevelConfig cfg)
    {
        if (cfg == null || cfg.monsters == null || cfg.monsters.Count == 0) return false;
        foreach (var entry in cfg.monsters)
        {
            if (entry != null && entry.monsterPrefab != null) return true;
        }
        return false;
    }

    /// <summary>从当前层的怪物列表中按权重随机选一个怪物条目</summary>
    public WaveMonsterEntry PickRandomMonsterEntry()
    {
        if (waveConfig == null) return null;

        var cfg = GetEffectiveWaveConfig();
        if (cfg == null) return null;
        return waveConfig.PickRandomMonster(cfg);
    }

    /// <summary>设置当前波次（恢复存档用），会重置本层计数</summary>
    public void SetWave(int wave)
    {
        currentWave = Mathf.Max(1, wave);
        _spawnedInWave = 0;
        _killedInWave = 0;
        _isInBreak = false;
        _breakTimer = 0f;

        // 【修复】读档后发布层数变化事件，通知HUD更新右上角显示
        EventBus.Instance.Publish(new WaveChangedEventArgs()
        {
            newWave = currentWave,
            difficultyFactor = GetDifficultyFactor()
        });
    }

    /// <summary>重置层数系统（新一局用）</summary>
    public void ResetSystem()
    {
        currentWave = 1;
        _spawnedInWave = 0;
        _killedInWave = 0;
        _isInBreak = false;
        _breakTimer = 0f;

        EventBus.Instance.Publish(new WaveChangedEventArgs()
        {
            newWave = currentWave,
            difficultyFactor = GetDifficultyFactor()
        });
    }
}
