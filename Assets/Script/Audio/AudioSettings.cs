using System;

/// <summary>
/// 音频设置数据 —— 由 SaveManager 持久化（JSON）
/// 只存用户可调节的音量值，不存 AudioClip 引用（那些在 AudioConfig 里）
/// </summary>
[Serializable]
public class AudioSettings
{
    /// <summary>BGM 音量 0~1</summary>
    public float bgmVolume = 0.6f;

    /// <summary>音效音量 0~1</summary>
    public float sfxVolume = 1f;
}
