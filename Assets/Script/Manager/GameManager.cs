using UnityEngine;

/// <summary>
/// 游戏管理器 —— 单例门面（Facade Pattern）
/// 
/// 【架构说明】
///   原 400+ 行的 GameManager 已按职责拆分为 3 个独立组件（同挂在一个 GameObject 上）：
///     ├─ GameStateController   状态机 + 暂停/恢复 + 时间冻结 + 切后台生命周期
///     ├─ GameSaveSystem        存档读写（SaveCurrentRun / ContinueSavedRun）
///     └─ GameFlowController    游戏流程 + 本局统计 + 玩家重置 + GameStartup缓存
/// 
///   GameManager 本身只做三件事：
///     1. 单例入口（BaseMonoSingleton）
///     2. Awake 时自动挂载/获取各子组件（GetOrAddComponent，确保一定存在）
///     3. 对外提供统一接口（转发给子组件），保持与原有代码 100% 兼容
/// 
/// 【对外兼容性】
///   所有原有的 public 属性和方法保持不变，其他脚本（PausePanel / PlayerAutoWeapon 等）无需修改：
///     - 属性：CurrentState, IsPaused, SurviveGameTime, TotalKills
///     - 流程：StartNewGame(), GameOver(), RestartGame()
///     - 暂停：PauseGame(), ResumeGame(), SetGameState()
///     - 存档：SaveCurrentRun(), ContinueSavedRun(), HasValidRunSave()
///     - 其他：ResetPlayerObject(), AddKill()
/// 
/// 【Inspector 配置】
///   playerSpawnPosition 仍在 GameManager 上配置，Awake 时自动同步到 GameFlowController
/// </summary>
public class GameManager : BaseMonoSingleton<GameManager>
{
    [Header("玩家初始出生点")]
    public Vector3 playerSpawnPosition = Vector3.zero;

    // ===== 子组件引用（Awake 时自动获取，没有则自动挂载）=====
    public GameStateController StateController { get; private set; }
    public GameSaveSystem SaveSystem { get; private set; }
    public GameFlowController FlowController { get; private set; }

    // ===== 转发属性（保持对外兼容，其他脚本直接读这些值）=====
    public GameState CurrentState => StateController.CurrentState;
    public bool IsPaused => StateController.IsPaused;
    public float SurviveGameTime => FlowController.SurviveGameTime;
    public int TotalKills => FlowController.TotalKills;

    protected override void Awake()
    {
        base.Awake();

        // 自动获取或挂载各子组件（企业级：GetOrAddComponent，确保组件一定存在，无需手动拖）
        StateController = GetOrAddComponent<GameStateController>();
        SaveSystem = GetOrAddComponent<GameSaveSystem>();
        FlowController = GetOrAddComponent<GameFlowController>();

        // 同步出生点配置到 FlowController（Inspector 只在 GameManager 上配一次）
        FlowController.playerSpawnPosition = playerSpawnPosition;
    }

    /// <summary>获取组件，没有则自动挂载（确保子组件一定存在）</summary>
    private T GetOrAddComponent<T>() where T : Component
    {
        T comp = GetComponent<T>();
        if (comp == null)
        {
            comp = gameObject.AddComponent<T>();
        }
        return comp;
    }

    // ===== 转发方法（保持对外兼容，其他脚本调用这些方法无需修改）=====

    /// <summary>设置游戏状态（外部一般不直接调用，用 StartNewGame/GameOver/RestartGame）</summary>
    public void SetGameState(GameState newState) => StateController.SetGameState(newState);

    /// <summary>暂停游戏 —— 独立 IsPaused 标志，不依赖 CurrentState，确保暂停一定生效</summary>
    /// <param name="silent">true=静默暂停（升级面板用，不弹暂停面板）</param>
    public void PauseGame(bool silent = false) => StateController.PauseGame(silent);

    /// <summary>恢复游戏</summary>
    /// <param name="silent">true=静默恢复（升级面板用）</param>
    public void ResumeGame(bool silent = false) => StateController.ResumeGame(silent);

    /// <summary>保存当前局进度（退出游戏/切后台/死亡时调用）</summary>
    public void SaveCurrentRun() => SaveSystem.SaveCurrentRun();

    /// <summary>从存档恢复本局进度（点"继续游戏"时调用）</summary>
    public void ContinueSavedRun() => SaveSystem.ContinueSavedRun();

    /// <summary>是否有可继续的局内存档</summary>
    public bool HasValidRunSave() => SaveSystem.HasValidRunSave();

    /// <summary>开始新一局游戏（首次启动用）</summary>
    public void StartNewGame() => FlowController.StartNewGame();

    /// <summary>游戏结束 —— 玩家死亡时调用</summary>
    public void GameOver() => FlowController.GameOver();

    /// <summary>重新开始 —— 从GameOver状态回到Playing</summary>
    public void RestartGame() => FlowController.RestartGame();

    /// <summary>重置玩家对象到初始状态（位置、血量、激活、组件同步）</summary>
    public void ResetPlayerObject() => FlowController.ResetPlayerObject();

    /// <summary>怪物死亡时调用，累计击杀数</summary>
    public void AddKill() => FlowController.AddKill();
}

/// <summary>游戏状态枚举（None / Playing / Paused / GameOver）</summary>
public enum GameState
{
    None,
    Playing,
    Paused,
    GameOver
}
