using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 波次配置编辑器工具
/// 菜单：Tools/波次系统/创建默认波次配置
/// 
/// 一键生成包含5层预设的WaveSystemConfig，自动找到项目中的怪物预制体并填入。
/// 企业标准数值：血量每层+15%，速度每层+5%，经验每层+10%。
/// </summary>
public static class WaveConfigEditor
{
    [MenuItem("Tools/波次系统/创建默认波次配置")]
    public static void CreateDefaultWaveConfig()
    {
        // 确保目录存在
        string folderPath = "Assets/Config";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Config");
        }

        string assetPath = $"{folderPath}/DefaultWaveConfig.asset";

        // 如果已存在，询问是否覆盖
        var existing = AssetDatabase.LoadAssetAtPath<WaveSystemConfig>(assetPath);
        if (existing != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "配置已存在",
                "DefaultWaveConfig.asset 已存在，是否覆盖？",
                "覆盖", "取消");
            if (!overwrite) return;
            AssetDatabase.DeleteAsset(assetPath);
        }

        // 自动查找项目中的怪物预制体
        GameObject monsterPrefab = FindMonsterPrefab();

        // 创建配置
        WaveSystemConfig config = ScriptableObject.CreateInstance<WaveSystemConfig>();
        config.waves = new List<WaveLevelConfig>();

        // 预设5层，企业标准难度递增
        // 血量：1.0 → 1.15 → 1.35 → 1.6 → 1.9（约每层+15%）
        // 速度：1.0 → 1.03 → 1.06 → 1.1 → 1.15（约每层+3~5%，别太快）
        // 经验：1.0 → 1.1 → 1.2 → 1.3 → 1.5（每层+10%）
        // 怪物总数：10 → 12 → 15 → 18 → 20（逐渐增多）
        // 同屏上限：30 → 40 → 50 → 60 → 80
        config.waves.Add(CreateWave("第1层 - 新手村",   total: 10, hp: 1.0f,  speed: 1.0f,  exp: 1.0f, maxAlive: 30, monsterPrefab));
        config.waves.Add(CreateWave("第2层 - 初见难度", total: 12, hp: 1.15f, speed: 1.03f, exp: 1.1f, maxAlive: 40, monsterPrefab));
        config.waves.Add(CreateWave("第3层 - 渐入佳境", total: 15, hp: 1.35f, speed: 1.06f, exp: 1.2f, maxAlive: 50, monsterPrefab));
        config.waves.Add(CreateWave("第4层 - 挑战升级", total: 18, hp: 1.6f,  speed: 1.1f,  exp: 1.3f, maxAlive: 60, monsterPrefab));
        config.waves.Add(CreateWave("第5层 - 精英来袭", total: 20, hp: 1.9f,  speed: 1.15f, exp: 1.5f, maxAlive: 80, monsterPrefab));

        // 无限模式参数（企业标准）
        config.infiniteHpGrowth = 0.15f;     // 每层多15%血
        config.infiniteSpeedGrowth = 0.05f;  // 每层多5%速度
        config.infiniteExpGrowth = 0.1f;     // 每层多10%经验

        // 保存资产
        AssetDatabase.CreateAsset(config, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 选中并高亮
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);

        string prefabInfo = monsterPrefab != null ? $"已自动填入怪物: {monsterPrefab.name}" : "⚠️ 未找到怪物预制体，请手动拖入";
        Debug.Log($"✅ 已创建默认波次配置：{assetPath}\n{prefabInfo}\n" +
                  "下一步：把 DefaultWaveConfig 拖到场景中 WaveSystem 组件的 WaveConfig 字段");
    }

    /// <summary>
    /// 自动查找项目中的怪物预制体
    /// 优先找名字含"Monster"的预制体
    /// </summary>
    private static GameObject FindMonsterPrefab()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab Monster");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<MonsterBase>() != null)
            {
                return prefab;
            }
        }

        // 降级：找任意有MonsterBase的预制体
        guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<MonsterBase>() != null)
            {
                return prefab;
            }
        }

        return null;
    }

    /// <summary>
    /// 创建一层配置
    /// </summary>
    private static WaveLevelConfig CreateWave(string name, int total, float hp, float speed, float exp, int maxAlive, GameObject monsterPrefab)
    {
        var wave = new WaveLevelConfig
        {
            waveName = name,
            monsters = new List<WaveMonsterEntry>(),
            totalMonsters = total,
            hpMultiplier = hp,
            speedMultiplier = speed,
            expMultiplier = exp,
            maxAliveOverride = maxAlive
        };

        // 如果找到了怪物预制体，自动添加
        if (monsterPrefab != null)
        {
            wave.monsters.Add(new WaveMonsterEntry
            {
                monsterPrefab = monsterPrefab,
                spawnWeight = 100,
                hpMultiplier = 1f,
                speedMultiplier = 1f
            });
        }

        return wave;
    }

    [MenuItem("Tools/波次系统/选中场景中的WaveSystem")]
    public static void SelectWaveSystem()
    {
        var ws = Object.FindObjectOfType<WaveSystem>();
        if (ws != null)
        {
            Selection.activeObject = ws.gameObject;
            EditorGUIUtility.PingObject(ws.gameObject);
        }
        else
        {
            EditorUtility.DisplayDialog("未找到", "场景中没有 WaveSystem 组件", "OK");
        }
    }
}
