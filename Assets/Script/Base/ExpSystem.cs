using UnityEngine;

public class ExpSystem : BaseMonoSingleton<ExpSystem>
{
    [Header("等级经验配置（拖进来，不拖就用默认值）")]
    public LevelProgressionConfig progressionConfig;

    private LevelProgressionConfig _defaultConfig;

    protected override void Awake()
    {
        base.Awake();
        if (progressionConfig == null)
        {
            _defaultConfig = ScriptableObject.CreateInstance<LevelProgressionConfig>();
            _defaultConfig.maxLevel = 99;
            _defaultConfig.baseExp = 12;
            _defaultConfig.expGrowFactor = 1.3f;
            Debug.LogWarning("[ExpSystem] 用默认配置");
        }
    }

    public LevelProgressionConfig GetConfig()
    {
        return progressionConfig != null ? progressionConfig : _defaultConfig;
    }

    public void AddExp(int expValue)
    {
        if (expValue <= 0) return;

        PlayerExp p = PlayerExp.Instance;
        p.curExp += expValue;
        LevelProgressionConfig config = GetConfig();

        // 关键：不要写 Publish<BaseEventArgs>，否则事件没人收到！
        EventBus.Instance.Publish(new AddExpEventArgs()
        {
            addValue = expValue,
            currentExp = p.curExp,
            nextLevelRequireExp = config.CalcNextLevelRequire(p.level)
        });

        CheckAndProcessLevelUp();
    }

    private void CheckAndProcessLevelUp()
    {
        PlayerExp p = PlayerExp.Instance;
        LevelProgressionConfig config = GetConfig();

        while (true)
        {
            int require = config.CalcNextLevelRequire(p.level);
            if (p.curExp < require) break;

            p.curExp -= require;
            p.level += 1;

            Debug.Log($"[ExpSystem] ⬆️ 升级！当前等级={p.level}，剩余经验={p.curExp}，下一级需要={config.CalcNextLevelRequire(p.level)}");

            EventBus.Instance.Publish(new PlayerLevelUpEventArgs()
            {
                newLevel = p.level,
                remainExp = p.curExp,
                nextRequireExp = config.CalcNextLevelRequire(p.level)
            });

            if (config.maxLevel > 0 && p.level >= config.maxLevel) break;
        }
    }

    public void ResetSystem() { }
}
