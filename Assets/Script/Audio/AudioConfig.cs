
using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 音效事件类型 —— 与 EventBus 事件一一对应
/// 新增音效就在这里加枚举，然后在 Inspector 里配 Clip
/// </summary>
public enum SfxType
{
    PlayerShoot,    // 默认射击音效（兼容旧配置）
    PistolShoot,    // 手枪射击
    RifleShoot,     // 步枪/AK射击
    GatlingShoot,   // 加特林射击
    ShotgunShoot,   // 霰弹枪射击
    LaserShoot,     // 激光射击
    ExpPickup,
    LevelUp,        // 升级音效
}
/// <summary>
/// BGM 类型 —— 按游戏状态划分
/// </summary>
public enum BgmType
{
    MainMenu,
    Battle,
    GameOver,
}
[Serializable]
public class BgmEntry
{
    public BgmType type;
    public AudioClip clip;
}

[Serializable]
public class SfxEntry
{
    public SfxType type;
    public AudioClip clip;
}
[CreateAssetMenu(fileName = "AudioConfig", menuName = "ScriptableObjects/AudioConfig", order = 1)]
public class AudioConfig : ScriptableObject
{
    [Header("BGM 配置")]
    public BgmEntry[] bgmList;

    [Header("SFX 配置")]
    public SfxEntry[] sfxList;
    // 运行时查表（避免每次遍历数组）
    private Dictionary<BgmType, AudioClip> _bgmDict;
    private Dictionary<SfxType, AudioClip> _sfxDict;
    public void InitAudio()
    {
        _bgmDict = new Dictionary<BgmType, AudioClip>();
        foreach (var entry in bgmList)
            if (entry.clip != null) _bgmDict[entry.type] = entry.clip;

        _sfxDict = new Dictionary<SfxType, AudioClip>();
        foreach (var entry in sfxList)
            if (entry.clip != null) _sfxDict[entry.type] = entry.clip;
    }
    public AudioClip GetBgm(BgmType type) =>
        _bgmDict != null && _bgmDict.TryGetValue(type, out var c) ? c : null;

    public AudioClip GetSfx(SfxType type) =>
        _sfxDict != null && _sfxDict.TryGetValue(type, out var c) ? c : null;
}
