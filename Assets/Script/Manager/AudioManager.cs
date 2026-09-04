using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : BaseMonoSingleton<AudioManager>
{
    #region 字段
    [Header("Addressables 路径名")]
    public string configAddress = "audio_config";
    private AudioConfig _config;
    public AudioConfig config => _config;


    [Header("BGM AudioSource（常驻，不需要池）")]
    public AudioSource bgmSource;

    [Header("音效池根节点")]
    public Transform sfxPoolRoot;

    [Header("音效池初始数量")]
    public int sfxPoolInitCount = 8;

    [Header("音效池最大数量（超过则丢弃新音效）")]
    public int sfxPoolMaxCount = 20;

    // 音量
    public float BgmVolume { get; private set; }
    public float SfxVolume { get; private set; }

    // 音效对象池（AudioManager 自维护轻量池，不依赖 PoolManager）
    private Queue<SfxPoolItem> _sfxPool;
    private int _aliveSfxCount;

    // 当前 BGM，防止重复切换
    private BgmType _currentBgm = BgmType.Battle;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        _config = AddressablesManager.Instance.LoadAssetSync<AudioConfig>(configAddress);
        if (_config == null)
        {
            Debug.LogError("[AudioManager] 音频配置加载失败！检查路径名是否是 audio_config");
        }
        else
        {
            Debug.Log($"[AudioManager] 音频配置加载成功，sfxList长度={_config.sfxList.Length}，bgmList长度={_config.bgmList.Length}");
            // 调试：打印每个已配置的音效类型，确认LevelUp是否在里面
            for (int i = 0; i < _config.sfxList.Length; i++)
            {
                var entry = _config.sfxList[i];
                Debug.Log($"[AudioManager] sfxList[{i}] type={entry.type}，clip={(entry.clip != null ? entry.clip.name : "NULL/Missing!")}");
            }
        }

        // 1. 加载音量设置
        LoadVolumeSettings();
        Debug.Log($"[AudioManager] 音量设置：BgmVolume={BgmVolume}，SfxVolume={SfxVolume}");

        // 2. 构建配置查表
        config?.InitAudio();

        // 3. 初始化音效池
        InitSfxPool();

        // 4. 监听 EventBus 事件（核心解耦点）
        RegisterEventListeners();

        Debug.Log("[AudioManager] Awake完成，已注册PlayerLevelUpEventArgs监听");
    }

    private void InitSfxPool()
    {
        _sfxPool = new Queue<SfxPoolItem>();
        _aliveSfxCount = 0;

        Transform root = sfxPoolRoot != null ? sfxPoolRoot : transform;

        for (int i = 0; i < sfxPoolInitCount; i++)
        {
            SfxPoolItem item = CreateSfxItem(root);
            _sfxPool.Enqueue(item);
        }
    }

    /// <summary>创建一个新的音效播放对象</summary>
    private SfxPoolItem CreateSfxItem(Transform parent)
    {
        GameObject go = new GameObject("Sfx_" + _aliveSfxCount);
        go.transform.SetParent(parent);
        go.SetActive(false);
        SfxPoolItem item = go.AddComponent<SfxPoolItem>();
        return item;
    }

    /// <summary>
    /// 从本地存档加载音量设置；没有存档则用默认值
    /// </summary>
    private void LoadVolumeSettings()
    {
        AudioSettings settings = SaveManager.Instance.Load<AudioSettings>();
        if (settings == null)
        {
            // 无存档，用 AudioSettings 默认值
            settings = new AudioSettings();


        }

        BgmVolume = settings.bgmVolume;
        SfxVolume = settings.sfxVolume;

        // 应用到 BGM 音源
        if (bgmSource != null)
        {
            bgmSource.volume = BgmVolume;
        }
    }

    /// <summary>
    /// 核心：所有业务音效通过 EventBus 触发，这里统一监听
    /// 业务层代码不需要知道 AudioManager 的存在
    /// </summary>
    private void RegisterEventListeners()
    {
        // 用方法引用而不是lambda，这样 OnDestroy 时可以取消订阅
        // （lambda 无法保存委托引用，切场景后会留死引用导致 MissingReferenceException）
        EventBus.Instance.Subscribe<BulletFiredEventArgs>(OnBulletFired);
        EventBus.Instance.Subscribe<PlayerLevelUpEventArgs>(OnPlayerLevelUp);
        EventBus.Instance.Subscribe<GameOverEventArgs>(OnGameOver);
    }

    /// <summary>取消所有 EventBus 订阅（OnDestroy 时调用，防止切场景后留死引用）</summary>
    private void UnregisterEventListeners()
    {
        EventBus.Instance.Unsubscribe<BulletFiredEventArgs>(OnBulletFired);
        EventBus.Instance.Unsubscribe<PlayerLevelUpEventArgs>(OnPlayerLevelUp);
        EventBus.Instance.Unsubscribe<GameOverEventArgs>(OnGameOver);
    }

    // ===== EventBus 回调方法（从 lambda 改成方法引用，便于取消订阅）=====
    private void OnBulletFired(BulletFiredEventArgs e) => PlaySfx(e.sfxType);
    private void OnPlayerLevelUp(PlayerLevelUpEventArgs e)
    {
        Debug.Log($"[AudioManager] 收到升级事件！newLevel={e.newLevel}，准备播放升级音效");
        // 升级音效是旋律性的，关闭 pitch 随机化防止走调；音量提 1.3 倍让它更突出
        PlaySfx(SfxType.LevelUp, volumeScale: 1.3f, randomizePitch: false);
    }
    private void OnGameOver(GameOverEventArgs _) => PlayBgm(BgmType.GameOver);

    /// <summary>
    /// 销毁时取消 EventBus 订阅
    /// 【关键】切场景时重复的 AudioManager 会被 Destroy，如果不取消订阅，
    /// EventBus 里会留着已销毁实例的死引用，子弹发射时报 MissingReferenceException
    /// </summary>
    private void OnDestroy()
    {
        UnregisterEventListeners();
    }

    /// <summary>
    /// 播放 BGM —— 带淡入淡出，同一首不重复播
    /// </summary>
    public void PlayBgm(BgmType type, float fadeDuration = 0.5f)
    {
        if (type == _currentBgm) return;
        _currentBgm = type;

        AudioClip clip = config.GetBgm(type);
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] 未找到 BGM: {type}");
            return;
        }

        StartCoroutine(FadeSwitchBgm(clip, fadeDuration));
    }

    private IEnumerator FadeSwitchBgm(AudioClip clip, float fadeDuration)
    {
        if (bgmSource == null) yield break;

        // 淡出当前 BGM
        float startVolume = bgmSource.volume;
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / fadeDuration);
            yield return null;
        }

        // 切换 Clip
        bgmSource.clip = clip;
        bgmSource.Play();

        // 淡入新 BGM
        float targetVolume = BgmVolume;
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, targetVolume, timer / fadeDuration);
            yield return null;
        }

        bgmSource.volume = targetVolume;
    }

    /// <summary>
    /// 播放音效 —— 从对象池取，pitch 轻微随机化避免重复感
    /// </summary>
    /// <param name="type">音效类型</param>
    /// <param name="volumeScale">音量倍率（1=正常，1.5=放大1.5倍）</param>
    /// <param name="randomizePitch">是否随机音调（射击音效建议true，旋律性音效如升级建议false）</param>
    public void PlaySfx(SfxType type, float volumeScale = 1f, bool randomizePitch = true)
    {
        if (config == null)
        {
            Debug.LogError($"[AudioManager] PlaySfx({type}) 失败：config为null！");
            return;
        }

        AudioClip clip = config.GetSfx(type);
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] PlaySfx({type}) 跳过：未找到对应AudioClip！请检查AudioConfig里是否配置了{type}的音效（可能是Missing引用）");
            return;
        }

        SfxPoolItem item = GetSfxFromPool();
        if (item == null)
        {
            Debug.LogWarning($"[AudioManager] PlaySfx({type}) 跳过：音效池已满（{_aliveSfxCount}/{sfxPoolMaxCount}）");
            return; // 池满了，跳过这一声
        }

        // pitch 随机 ±10%，连续射击不会像机关枪一样机械
        // 旋律性音效（升级等）不要随机，否则会走调、发闷、不清晰
        float pitch = randomizePitch ? Random.Range(0.9f, 1.1f) : 1f;
        float finalVolume = SfxVolume * volumeScale;
        Debug.Log($"[AudioManager] 播放音效 type={type}，clip={clip.name}，音量={finalVolume}，pitch={pitch}，随机pitch={randomizePitch}");
        item.Play(clip, finalVolume, pitch, OnSfxFinished);
    }

    private SfxPoolItem GetSfxFromPool()
    {
        if (_sfxPool.Count > 0)
        {
            SfxPoolItem item = _sfxPool.Dequeue();
            item.gameObject.SetActive(true);
            _aliveSfxCount++;
            return item;
        }

        // 池空了，动态扩容（不超过上限）
        if (_aliveSfxCount < sfxPoolMaxCount)
        {
            Transform root = sfxPoolRoot != null ? sfxPoolRoot : transform;
            SfxPoolItem item = CreateSfxItem(root);
            item.gameObject.SetActive(true);
            _aliveSfxCount++;
            return item;
        }

        // 达到上限，丢弃这次音效
        return null;
    }

    /// <summary>音效播完后回池</summary>
    private void OnSfxFinished(SfxPoolItem item)
    {
        if (item == null) return;
        item.ResetState();
        item.gameObject.SetActive(false);
        _sfxPool.Enqueue(item);
        _aliveSfxCount--;
    }


    /// <summary>设置 BGM 音量（0~1），实时生效并持久化</summary>
    public void SetBgmVolume(float v)
    {
        BgmVolume = Mathf.Clamp01(v);
        ApplyBgmVolume();
        SaveVolumeSettings();
    }

    /// <summary>设置音效音量（0~1），实时生效并持久化</summary>
    public void SetSfxVolume(float v)
    {
        SfxVolume = Mathf.Clamp01(v);
        // 音效是即时播放的，下次 PlaySfx 时会用新值，这里不需要额外操作
        SaveVolumeSettings();
    }

    /// <summary>把当前音量应用到 BGM 音源（音效在 PlaySfx 时实时计算）</summary>
    private void ApplyBgmVolume()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = BgmVolume;
        }
    }

    /// <summary>持久化音量设置到本地（JSON）</summary>
    private void SaveVolumeSettings()
    {
        SaveManager.Instance.Save(new AudioSettings
        {
            bgmVolume = BgmVolume,
            sfxVolume = SfxVolume
        });
    }

}
