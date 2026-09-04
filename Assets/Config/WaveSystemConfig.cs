using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单层中的一种怪物条目
/// 配置该层可以生成哪些怪物，以及各自的生成权重
/// </summary>
[Serializable]
public class WaveMonsterEntry
{
    [Tooltip("怪物预制体（必须挂MonsterBase）")]
    public GameObject monsterPrefab;

    [Tooltip("生成权重，值越大越容易被随机到")]
    [Range(1, 100)] public int spawnWeight = 50;

    [Tooltip("该怪物在本层的血量倍率（在全局层数倍率基础上再乘）")]
    public float hpMultiplier = 1f;

    [Tooltip("该怪物在本层的速度倍率")]
    public float speedMultiplier = 1f;
}

/// <summary>
/// 单层（一波）的完整配置
/// </summary>
[Serializable]
public class WaveLevelConfig
{
    [Tooltip("层名称，仅用于Inspector可读性")]
    public string waveName = "第1层";

    [Tooltip("本层可生成的怪物列表（按权重随机选）")]
    public List<WaveMonsterEntry> monsters = new List<WaveMonsterEntry>();

    [Tooltip("本层固定怪物总数（清完所有怪才进下一层）")]
    public int totalMonsters = 10;

    [Tooltip("清完本层后缓冲多久再进下一层（秒）")]
    public float breakTime = 3f;

    [Tooltip("本层全局血量倍率（所有怪物都乘）")]
    public float hpMultiplier = 1f;

    [Tooltip("本层全局速度倍率")]
    public float speedMultiplier = 1f;

    [Tooltip("本层经验掉落倍率")]
    public float expMultiplier = 1f;

    [Tooltip("本层同时存活的最大怪物数（0=用Spawner全局值）")]
    public int maxAliveOverride = 0;
}

/// <summary>
/// 波次系统总配置 —— ScriptableObject
/// 创建路径：Assets/Configs/WaveSystemConfig
/// 
/// 【设计思路】
///   - 前 N 层用 waves 列表精确配置（每层怪物种类、数量、难度）
///   - 超出配置层数后进入无限模式，按 infiniteHpGrowth / infiniteSpeedGrowth 线性增长
///   - 怪物种类：无限模式复用最后一层的怪物列表
/// </summary>
[CreateAssetMenu(fileName = "WaveSystemConfig", menuName = "Configs/WaveSystemConfig")]
public class WaveSystemConfig : ScriptableObject
{
    [Header("分层配置（按顺序执行）")]
    public List<WaveLevelConfig> waves = new List<WaveLevelConfig>();

    [Header("超出配置层数后的无限模式")]
    [Tooltip("无限模式每层血量增长系数（在最后一层基础上递增）")]
    public float infiniteHpGrowth = 0.15f;

    [Tooltip("无限模式每层速度增长系数")]
    public float infiniteSpeedGrowth = 0.08f;

    [Tooltip("无限模式每层经验增长系数")]
    public float infiniteExpGrowth = 0.1f;

    /// <summary>
    /// 根据层数索引获取对应配置
    /// waveIndex 从 0 开始（第1层 = index 0）
    /// 超出配置范围返回 null，调用方用无限模式逻辑
    /// </summary>
    public WaveLevelConfig GetWaveConfig(int waveIndex)
    {
        if (waves == null || waves.Count == 0) return null;
        if (waveIndex < 0) return null;
        if (waveIndex < waves.Count) return waves[waveIndex];
        return null; // 超出配置，走无限模式
    }

    /// <summary>
    /// 获取最后一层的配置（无限模式复用其怪物列表）
    /// </summary>
    public WaveLevelConfig GetLastWaveConfig()
    {
        if (waves == null || waves.Count == 0) return null;
        return waves[waves.Count - 1];
    }

    /// <summary>
    /// 从一层的怪物列表中按权重随机选一个怪物条目
    /// 【修复】所有条目权重都为0时，改为等概率随机（而不是固定返回第一个）
    /// </summary>
    public WaveMonsterEntry PickRandomMonster(WaveLevelConfig waveConfig)
    {
        if (waveConfig == null || waveConfig.monsters == null || waveConfig.monsters.Count == 0)
            return null;

        // 过滤掉无效条目
        List<WaveMonsterEntry> validEntries = new List<WaveMonsterEntry>();
        foreach (var entry in waveConfig.monsters)
        {
            if (entry != null && entry.monsterPrefab != null)
                validEntries.Add(entry);
        }
        if (validEntries.Count == 0) return null;

        // 只有一种怪物直接返回
        if (validEntries.Count == 1) return validEntries[0];

        // 计算总权重
        int totalWeight = 0;
        foreach (var entry in validEntries)
        {
            totalWeight += Mathf.Max(0, entry.spawnWeight);
        }

        // 所有权重都为0 → 等概率随机
        if (totalWeight <= 0)
        {
            return validEntries[UnityEngine.Random.Range(0, validEntries.Count)];
        }

        // 按权重随机
        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var entry in validEntries)
        {
            cumulative += Mathf.Max(0, entry.spawnWeight);
            if (roll < cumulative)
                return entry;
        }

        return validEntries[validEntries.Count - 1];
    }
}
