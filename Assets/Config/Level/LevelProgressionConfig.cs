
using UnityEngine;
[CreateAssetMenu(fileName = "LevelProgressionConfig", menuName = "Configs/LevelProgressionConfig")]
public class LevelProgressionConfig : ScriptableObject
{
    [Header("等级上限，0=无上限")]
    public int maxLevel = 99;

    [Header("1级升2级基础经验")]
    public int baseExp = 12;

    [Header("经验增长系数，每一级需要多少倍上一级")]
    public float expGrowFactor = 1.3f;
    public int CalcNextLevelRequire(int currentLevel)
    {
        if (currentLevel < maxLevel)
        {
            if (maxLevel > 0 && currentLevel >= maxLevel)
                return int.MaxValue;
        }
        float val = baseExp * Mathf.Pow(expGrowFactor, currentLevel - 1);
        return Mathf.RoundToInt(val);
    }
}