using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

/// <summary>
/// Addressables 资源加载管理器 —— 企业级最小可行版本
/// 
/// 【设计原则】
///   1. 任何一步失败都不能卡死，必须通过 onError 通知上层
///   2. 网络抖动自动重试（默认2次），超过上限才报错
///   3. 启动时预下载全部 Remote 资源，不依赖标签，避免漏下载
///   4. 运行时加载走缓存，配置表常驻内存
///   5. 【IL2CPP安全】启动时把所有关键资源预加载到内存缓存，
///      运行时 LoadAssetSync 先查缓存、命中直接返回，绝不调用 WaitForCompletion
///      （WaitForCompletion 在 IL2CPP 构建中会阻塞主线程形成死锁）
/// 
/// 【用法】
///   // 启动界面调用：初始化+检查更新+下载+预加载到内存
///   AddressablesManager.Instance.InitAndDownload(
///       status => statusText.text = status,
///       progress => slider.value = progress,
///       () => AddressablesManager.Instance.LoadSceneAsync("StartScene", null),
///       error => { errorText.text = error; retryBtn.SetActive(true); }
///   );
///   
///   // 运行时同步加载（资源已预加载到缓存，不会阻塞、IL2CPP安全）
///   var config = AddressablesManager.Instance.LoadAssetSync<WeaponListConfig>("weapon_list");
///   
///   // 运行时异步加载（未预加载的资源必须用这个）
///   AddressablesManager.Instance.LoadAssetAsync<GameObject>("monster", go => Instantiate(go));
///   
///   // 场景切换（场景必须在 Addressables 分组中，不能再用 SceneManager.LoadScene）
///   AddressablesManager.Instance.LoadSceneAsync("GameScene", () => Debug.Log("进入游戏"));
/// </summary>
public class AddressablesManager : BaseMonoSingleton<AddressablesManager>
{
    [Header("热更新配置")]
    [Tooltip("最大自动重试次数（网络失败时）")]
    public int maxRetry = 2;

    [Header("【IL2CPP必需】启动时预加载到内存的资源Key列表")]
    [Tooltip("把所有在Awake/Start里通过LoadAssetSync加载的资源Key填到这里，启动时会异步加载到缓存，避免IL2CPP下WaitForCompletion死锁")]
    public List<string> preloadAssetKeys = new List<string>();

    [Header("【IL2CPP必需】启动时预加载到内存的Label列表")]
    [Tooltip("把所有通过LoadAssetsByLabelSync加载的Label填到这里")]
    public List<string> preloadLabels = new List<string>();

    // 缓存已加载的资源对象（配置表常驻内存，不需要释放）
    private Dictionary<string, Object> _cache = new Dictionary<string, Object>();

    // 按Label批量加载的结果缓存（key=label名，value=IList）
    private Dictionary<string, object> _labelCache = new Dictionary<string, object>();

    // 自己跟踪初始化状态（Addressables 1.22.3 没有 IsInitialized 属性）
    private static bool _isInitialized = false;

    /// <summary>
    /// 重写Awake：如果Inspector未配置预加载列表，则自动填充当前项目默认用到的资源Key。
    /// 防止单例自动创建时列表为空导致IL2CPP下全部LoadAssetSync返回null。
    /// 新增资源时请同步更新这里的默认列表，或直接在Inspector里配置。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        if (preloadAssetKeys == null || preloadAssetKeys.Count == 0)
        {
            preloadAssetKeys = new List<string>
            {
                "武器列表",       // WeaponManager
                "wave_config",    // WaveSystem
                "audio_config",   // AudioManager
                "store_item_view",// BagAndStoreManager 商店卡片预制体
                "bag_item_view",  // BagAndStoreManager 背包卡片预制体
                "exp_orb_config", // ExpOrbManager
                "exp_orb_prefab", // ExpOrbManager
                "Monster"         // MonsterSpawner 兼容模式怪物
            };
            Debug.Log("[AddressablesManager] Awake：preloadAssetKeys 未配置，已自动填充默认列表");
        }

        if (preloadLabels == null || preloadLabels.Count == 0)
        {
            preloadLabels = new List<string>
            {
                "store_item"      // BagAndStoreManager 商店商品配置
            };
            Debug.Log("[AddressablesManager] Awake：preloadLabels 未配置，已自动填充默认列表");
        }
    }

    #region 启动流程：初始化 + 检查更新 + 预下载

    /// <summary>
    /// 初始化 + 检查更新 + 预下载全部 Remote 资源
    /// 企业标准：任何一步失败都走 onError，绝不静默卡死
    /// </summary>
    /// <param name="onStatus">状态文字回调</param>
    /// <param name="onProgress">下载进度 0~1</param>
    /// <param name="onComplete">全部成功完成</param>
    /// <param name="onError">任意一步失败，参数是可读的错误信息</param>
    public void InitAndDownload(
        System.Action<string> onStatus,
        System.Action<float> onProgress,
        System.Action onComplete,
        System.Action<string> onError = null)
    {
        StartCoroutine(InitAndDownloadCoroutine(onStatus, onProgress, onComplete, onError, maxRetry, 0));
    }

    private IEnumerator InitAndDownloadCoroutine(
        System.Action<string> onStatus,
        System.Action<float> onProgress,
        System.Action onComplete,
        System.Action<string> onError,
        int retryMax,
        int retryCount)
    {
        // ====== 第1步：初始化 Addressables ======
        // 关键修复：Addressables 1.22.3 中，如果内部已初始化，InitializeAsync() 会返回 invalid handle
        // 访问 Status 会抛 "Attempting to use an invalid operation handle" 异常
        // 所以先通过 ResourceLocators 判断内部是否已初始化，避免重复调用
        bool addressablesAlreadyInit = Addressables.ResourceLocators != null && Addressables.ResourceLocators.Any();
        if (!_isInitialized && !addressablesAlreadyInit)
        {
            onStatus?.Invoke("正在初始化...");
            var initHandle = Addressables.InitializeAsync();
            // yield 必须在 try-catch 外面（C# 语法限制：try块里不能yield）
            yield return initHandle;

            // 用 try-catch 包裹访问 Status：invalid handle 访问属性会抛异常
            AsyncOperationStatus initStatus = AsyncOperationStatus.None;
            System.Exception initOpError = null;
            try
            {
                initStatus = initHandle.Status;
                initOpError = initHandle.OperationException;
            }
            catch (System.Exception e)
            {
                // handle 无效时访问 Status 会抛异常，此时检查 ResourceLocators 判断是否实际已初始化
                Debug.LogWarning($"[AddressablesManager] 访问 initHandle 时异常（可能是已初始化的同步handle）：{e.Message}");
                addressablesAlreadyInit = Addressables.ResourceLocators != null && Addressables.ResourceLocators.Any();
                if (addressablesAlreadyInit)
                {
                    initStatus = AsyncOperationStatus.Succeeded;
                    Debug.Log("[AddressablesManager] handle 无效但 ResourceLocators 已有内容，判定为已初始化成功");
                }
                else
                {
                    initStatus = AsyncOperationStatus.Failed;
                    initOpError = e;
                }
            }

            int locatorCount = Addressables.ResourceLocators != null ? Addressables.ResourceLocators.Count() : 0;
            Debug.Log($"[AddressablesManager] 初始化结果：Status={initStatus}, OpError={initOpError}, LocatorCount={locatorCount}");
            bool initOk = initStatus == AsyncOperationStatus.Succeeded;
            if (!initOk)
            {
                Debug.LogError($"[AddressablesManager] 初始化失败！Status={initStatus}, OpError={initOpError}");

                if (retryCount < retryMax)
                {
                    Debug.LogWarning($"[AddressablesManager] 初始化失败，第{retryCount + 1}次重试...");
                    StartCoroutine(InitAndDownloadCoroutine(onStatus, onProgress, onComplete, onError, retryMax, retryCount + 1));
                    yield break;
                }
                onError?.Invoke("资源初始化失败，请检查网络后重试");
                yield break;
            }
            _isInitialized = true;
            Debug.Log("[AddressablesManager] 初始化完成");
        }
        else
        {
            _isInitialized = true;
            Debug.Log("[AddressablesManager] 已初始化，跳过");
        }

        // ====== 第2步：检查 catalog 更新 ======
        onStatus?.Invoke("正在检查更新...");
        var checkHandle = Addressables.CheckForCatalogUpdates(false);
        yield return checkHandle;

        if (checkHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Addressables.Release(checkHandle);
            if (retryCount < retryMax)
            {
                Debug.LogWarning($"[AddressablesManager] 检查更新失败，第{retryCount + 1}次重试...");
                StartCoroutine(InitAndDownloadCoroutine(onStatus, onProgress, onComplete, onError, retryMax, retryCount + 1));
                yield break;
            }
            onError?.Invoke("检查更新失败，请检查网络后重试");
            yield break;
        }

        // 有更新则更新 catalog
        if (checkHandle.Result != null && checkHandle.Result.Count > 0)
        {
            Debug.Log($"[AddressablesManager] 发现 {checkHandle.Result.Count} 个 catalog 更新");
            onStatus?.Invoke("发现新版本，正在更新...");

            var updateHandle = Addressables.UpdateCatalogs(checkHandle.Result, false);
            yield return updateHandle;
            Addressables.Release(checkHandle);

            if (updateHandle.Status != AsyncOperationStatus.Succeeded)
            {
                if (retryCount < retryMax)
                {
                    Debug.LogWarning($"[AddressablesManager] 更新 catalog 失败，第{retryCount + 1}次重试...");
                    StartCoroutine(InitAndDownloadCoroutine(onStatus, onProgress, onComplete, onError, retryMax, retryCount + 1));
                    yield break;
                }
                onError?.Invoke("更新资源目录失败，请检查网络后重试");
                yield break;
            }
            Debug.Log("[AddressablesManager] catalog 更新完成");
        }
        else
        {
            Debug.Log("[AddressablesManager] 已是最新版本，无需更新");
            onStatus?.Invoke("已是最新版本");
            Addressables.Release(checkHandle);
        }

        // ====== 第3步：预下载全部 Remote 资源 ======
        onStatus?.Invoke("正在下载资源...");
        onProgress?.Invoke(0f);

        // 收集所有资源定位器中的全部 key，不依赖标签，避免漏下载
        var allKeys = new List<object>();
        foreach (var locator in Addressables.ResourceLocators)
        {
            foreach (var key in locator.Keys)
            {
                allKeys.Add(key);
            }
        }
        Debug.Log($"[AddressablesManager] 准备下载 {allKeys.Count} 个资源的依赖包");

        // Union 模式：合并所有 key 的依赖，一次性下载
        // 注意：传 IList<object> 时必须指定 MergeMode，否则会抛 InvalidKeyException: No MergeMode is set
        // autoReleaseHandle=false：下载完成后不自动释放，方便后续加载资源时复用缓存
        var downloadHandle = Addressables.DownloadDependenciesAsync(allKeys, Addressables.MergeMode.Union, false);

        // 实时上报进度
        while (!downloadHandle.IsDone)
        {
            onProgress?.Invoke(downloadHandle.PercentComplete);
            yield return null;
        }
        onProgress?.Invoke(1f);

        if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
        {
            if (retryCount < retryMax)
            {
                Debug.LogWarning($"[AddressablesManager] 下载失败，第{retryCount + 1}次重试...");
                StartCoroutine(InitAndDownloadCoroutine(onStatus, onProgress, onComplete, onError, retryMax, retryCount + 1));
                yield break;
            }
            onError?.Invoke("资源下载失败，请检查网络后重试");
            yield break;
        }

        Debug.Log("[AddressablesManager] 全部资源预下载完成");
        onStatus?.Invoke("下载完成");

        // ====== 第4步：预加载关键资源到内存缓存（IL2CPP死锁防护核心）======
        // 必须在跳转场景前完成，否则下一个场景的Manager在Awake/Start里调用LoadAssetSync
        // 会走到 WaitForCompletion，在IL2CPP下阻塞主线程死锁。
        onStatus?.Invoke("正在加载资源...");
        yield return PreloadAssetsCoroutine();
        Debug.Log("[AddressablesManager] 内存预加载完成");

        onStatus?.Invoke("准备就绪");
        onComplete?.Invoke();
    }

    #endregion

    #region 启动预加载（IL2CPP死锁防护）

    /// <summary>
    /// 把 preloadAssetKeys 和 preloadLabels 中配置的资源全部异步加载到内存缓存。
    /// 必须在跳转业务场景前调用，确保后续 LoadAssetSync / LoadAssetsByLabelSync 直接命中缓存。
    /// 任何单个资源加载失败只打Warning，不中断整体流程（避免一个配错的key卡住整个游戏）。
    /// </summary>
    private IEnumerator PreloadAssetsCoroutine()
    {
        int total = preloadAssetKeys.Count + preloadLabels.Count;
        int done = 0;
        Debug.Log($"[AddressablesManager] 开始内存预加载，共 {total} 项");

        // 1. 预加载单个资源
        foreach (string key in preloadAssetKeys)
        {
            done++;
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[AddressablesManager] preloadAssetKeys 中存在空字符串，已跳过");
                continue;
            }
            if (_cache.ContainsKey(key))
            {
                Debug.Log($"[AddressablesManager] 预加载跳过（已缓存）：{key}");
                continue;
            }

            var handle = Addressables.LoadAssetAsync<Object>(key);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _cache[key] = handle.Result;
                Debug.Log($"[AddressablesManager] 预加载成功 [{done}/{total}]：{key}");
            }
            else
            {
                Debug.LogWarning($"[AddressablesManager] 预加载失败 [{done}/{total}]：{key}，" +
                    $"请检查该Key是否存在于Addressables分组中。后续LoadAssetSync将返回null。");
                Addressables.Release(handle);
            }
        }

        // 2. 预加载 Label 批量资源
        foreach (string label in preloadLabels)
        {
            done++;
            if (string.IsNullOrEmpty(label))
            {
                Debug.LogWarning("[AddressablesManager] preloadLabels 中存在空字符串，已跳过");
                continue;
            }
            if (_labelCache.ContainsKey(label))
            {
                Debug.Log($"[AddressablesManager] 预加载Label跳过（已缓存）：{label}");
                continue;
            }

            var handle = Addressables.LoadAssetsAsync<Object>(label, null);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _labelCache[label] = handle.Result;
                Debug.Log($"[AddressablesManager] 预加载Label成功 [{done}/{total}]：{label}，数量={handle.Result.Count}");
            }
            else
            {
                Debug.LogWarning($"[AddressablesManager] 预加载Label失败 [{done}/{total}]：{label}，" +
                    $"请检查该Label是否存在。后续LoadAssetsByLabelSync将返回空列表。");
                Addressables.Release(handle);
            }
        }

        Debug.Log($"[AddressablesManager] 内存预加载全部完成，缓存资源数={_cache.Count}，缓存Label数={_labelCache.Count}");
    }

    /// <summary>
    /// 检查某个资源是否已在内存缓存中（Manager初始化前可用来判断是否需要走异步）
    /// </summary>
    public bool IsAssetCached(string key)
    {
        return !string.IsNullOrEmpty(key) && _cache.ContainsKey(key);
    }

    /// <summary>
    /// 检查某个Label是否已在内存缓存中
    /// </summary>
    public bool IsLabelCached(string label)
    {
        return !string.IsNullOrEmpty(label) && _labelCache.ContainsKey(label);
    }

    #endregion

    #region 运行时资源加载

    /// <summary>
    /// 同步加载资源（启动预加载完成后调用，直接从缓存返回，IL2CPP安全）
    /// 【IL2CPP重要】缓存未命中时不会调用 WaitForCompletion（会死锁），而是返回 null 并打错误。
    /// 解决办法：把该 key 加入 AddressablesManager 的 preloadAssetKeys 列表，或改用 LoadAssetAsync。
    /// 加载失败返回 null 并打错误日志
    /// </summary>
    public T LoadAssetSync<T>(string key) where T : Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("[AddressablesManager] key 为空，无法加载");
            return null;
        }

        // 已缓存，直接返回（IL2CPP安全路径：不碰 WaitForCompletion）
        if (_cache.TryGetValue(key, out Object cached))
        {
            return cached as T;
        }

#if UNITY_EDITOR
        // 编辑器下切到 Android/iOS 平台 + Use Existing Build 时，远程下载的 WaitForCompletion 会死锁主线程
        // 因此移动平台编辑器模式下禁止同步等待，强制走预加载缓存
        bool isMobileBuildTarget =
            UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android ||
            UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS;
        if (isMobileBuildTarget)
        {
            Debug.LogError($"[AddressablesManager] 移动平台编辑器模式下禁止同步加载（防死锁）：{key}。" +
                "请将此key加入 preloadAssetKeys 列表，启动时预加载。当前返回 null。");
            return null;
        }

        // 编辑器 PC 平台：允许同步等待，方便快速迭代
        Debug.LogWarning($"[AddressablesManager] LoadAssetSync 缓存未命中，编辑器模式下同步加载：{key}。" +
            "建议将此key加入 preloadAssetKeys 以提升性能并保证IL2CPP兼容。");
        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
        handle.WaitForCompletion();

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _cache[key] = handle.Result;
            Debug.Log($"[AddressablesManager] 加载成功：{key}");
            return handle.Result;
        }
        else
        {
            Debug.LogError($"[AddressablesManager] 加载失败：{key}，请检查资源是否已上传到 CCD 或地址是否正确");
            Addressables.Release(handle);
            return null;
        }
#else
        // IL2CPP / 构建版本：禁止 WaitForCompletion，会死锁主线程
        Debug.LogError($"[AddressablesManager] IL2CPP下LoadAssetSync缓存未命中：{key}。" +
            "请将此key加入 AddressablesManager 的 preloadAssetKeys 列表（启动时预加载），" +
            "或改用 LoadAssetAsync 异步加载。当前返回 null。");
        return null;
#endif
    }

    /// <summary>
    /// 异步加载资源（运行时用，不卡主线程）
    /// </summary>
    public void LoadAssetAsync<T>(string key, System.Action<T> onComplete) where T : Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("[AddressablesManager] key 为空，无法加载");
            onComplete?.Invoke(null);
            return;
        }

        // 已缓存，直接回调
        if (_cache.TryGetValue(key, out Object cached))
        {
            onComplete?.Invoke(cached as T);
            return;
        }

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
        handle.Completed += op =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                _cache[key] = op.Result;
                Debug.Log($"[AddressablesManager] 异步加载成功：{key}");
                onComplete?.Invoke(op.Result);
            }
            else
            {
                Debug.LogError($"[AddressablesManager] 异步加载失败：{key}");
                Addressables.Release(op);
                onComplete?.Invoke(null);
            }
        };
    }

    /// <summary>
    /// 按 Label 同步批量加载资源（比如所有商店商品配置）
    /// 【IL2CPP重要】缓存未命中时不会调用 WaitForCompletion（会死锁），而是返回空列表并打错误。
    /// 解决办法：把该 label 加入 AddressablesManager 的 preloadLabels 列表。
    /// </summary>
    public List<T> LoadAssetsByLabelSync<T>(string label) where T : Object
    {
        if (string.IsNullOrEmpty(label))
        {
            Debug.LogError("[AddressablesManager] label 为空");
            return new List<T>();
        }

        // 已缓存，直接返回（IL2CPP安全路径）
        if (_labelCache.TryGetValue(label, out object cachedList))
        {
            List<T> result = new List<T>();
            if (cachedList is IList rawList)
            {
                foreach (var item in rawList)
                {
                    if (item is T typed) result.Add(typed);
                }
            }
            Debug.Log($"[AddressablesManager] 按Label从缓存加载：{label}，数量={result.Count}");
            return result;
        }

#if UNITY_EDITOR
        // 编辑器下切到 Android/iOS 平台 + Use Existing Build 时，远程下载的 WaitForCompletion 会死锁主线程
        bool isMobileBuildTarget =
            UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android ||
            UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS;
        if (isMobileBuildTarget)
        {
            Debug.LogError($"[AddressablesManager] 移动平台编辑器模式下禁止同步加载Label（防死锁）：{label}。" +
                "请将此label加入 preloadLabels 列表，启动时预加载。当前返回空列表。");
            return new List<T>();
        }

        // 编辑器 PC 平台：允许同步等待
        Debug.LogWarning($"[AddressablesManager] LoadAssetsByLabelSync 缓存未命中，编辑器模式下同步加载：{label}。" +
            "建议将此label加入 preloadLabels 以保证IL2CPP兼容。");
        AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(label, null);
        handle.WaitForCompletion();
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _labelCache[label] = handle.Result;
            Debug.Log($"[AddressablesManager] 按Label批量加载成功：{label}，数量={handle.Result.Count}");
            return new List<T>(handle.Result);
        }
        else
        {
            Debug.LogError($"[AddressablesManager] 按Label批量加载失败：{label}");
            Addressables.Release(handle);
            return new List<T>();
        }
#else
        // IL2CPP / 构建版本：禁止 WaitForCompletion
        Debug.LogError($"[AddressablesManager] IL2CPP下LoadAssetsByLabelSync缓存未命中：{label}。" +
            "请将此label加入 AddressablesManager 的 preloadLabels 列表（启动时预加载）。当前返回空列表。");
        return new List<T>();
#endif
    }

    /// <summary>
    /// 清空缓存（切场景时调用，配置表常驻内存一般不需要清）
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _labelCache.Clear();
        Debug.Log("[AddressablesManager] 缓存已清空");
    }

    #endregion

    #region 场景加载（Unity原生 SceneManager，不再走 Addressables）

    /// <summary>
    /// 异步加载场景（改用 Unity 原生 SceneManager，场景必须在 Build Settings 中启用）
    /// 
    /// 【改造说明】
    ///   场景已从 Addressables 分组移除，改回 Build Settings 本地场景。
    ///   因此跳转场景用 SceneManager.LoadSceneAsync，不再用 Addressables.LoadSceneAsync。
    ///   接口签名保持不变，上层调用代码无需修改。
    /// 
    /// 【用法】
    ///   AddressablesManager.Instance.LoadSceneAsync("GameScene", () => {
    ///       Debug.Log("进入游戏场景");
    ///   });
    /// </summary>
    /// <param name="sceneName">场景名（必须在 Build Settings 中启用，如 "GameScene" / "StartScene"）</param>
    /// <param name="onComplete">加载完成回调</param>
    /// <param name="onError">加载失败回调，参数是可读错误信息</param>
    /// <param name="additive">是否叠加加载（默认 false = 替换当前场景）</param>
    public void LoadSceneAsync(string sceneName, System.Action onComplete, System.Action<string> onError = null, bool additive = false)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[AddressablesManager] LoadSceneAsync: sceneName 为空");
            onError?.Invoke("场景名为空");
            return;
        }

        Debug.Log($"[AddressablesManager] 开始异步加载场景（SceneManager原生方式）：{sceneName}, additive={additive}");

        LoadSceneMode mode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;

        // 改用 Unity 原生 SceneManager 加载，不再依赖 Addressables
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName, mode);

        if (asyncOp == null)
        {
            Debug.LogError($"[AddressablesManager] 场景加载失败：{sceneName}，SceneManager返回null，请检查该场景是否已添加到Build Settings并启用");
            onError?.Invoke($"场景加载失败：{sceneName}，请检查该场景是否已添加到Build Settings并启用");
            return;
        }

        asyncOp.completed += op =>
        {
            Debug.Log($"[AddressablesManager] 场景加载成功：{sceneName}");
            onComplete?.Invoke();
        };
    }

    #endregion
}
