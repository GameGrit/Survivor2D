using UnityEngine;

/// <summary>
/// 音效对象池项 —— 挂在池中的 GameObject 上
/// 播放结束后自动回收到 AudioManager
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SfxPoolItem : MonoBehaviour
{
    private AudioSource _source;
    private System.Action<SfxPoolItem> _onFinished;

    private void Awake()
    {

        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        // 关键：2D 游戏音效用 spatialBlend=0，不随距离衰减
        // （默认是 1=3D，会导致音效池位置离 Camera 远时几乎听不到）
        _source.spatialBlend = 0f;
        // 关键：暂停游戏(timeScale=0)时音效不能停，所以忽略时间缩放
        _source.ignoreListenerPause = true;
    }

    /// <summary>
    /// 播放音效
    /// </summary>
    /// <param name="clip">音频片段</param>
    /// <param name="volume">音量（已乘 Master/Sfx）</param>
    /// <param name="pitch">音调（随机化用）</param>
    /// <param name="onFinished">播完回调（回池用）</param>
    public void Play(AudioClip clip, float volume, float pitch, System.Action<SfxPoolItem> onFinished = null)
    {
        _onFinished = onFinished;
        _source.clip = clip;
        _source.volume = volume;
        _source.pitch = pitch;
        _source.Play();

        // 用协程等播放结束，不用 Invoke（受 timeScale 影响）
        StartCoroutine(WaitForFinish(clip.length));
    }

    private System.Collections.IEnumerator WaitForFinish(float duration)
    {
        // 用 WaitForSecondsRealtime，不受 timeScale=0 影响
        yield return new WaitForSecondsRealtime(duration);
        _onFinished?.Invoke(this);
    }

    public void ResetState()
    {
        _source.Stop();
        _source.clip = null;
        _onFinished = null;
    }
}
