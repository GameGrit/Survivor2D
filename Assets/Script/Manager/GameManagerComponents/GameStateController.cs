using UnityEngine;

/// <summary>
/// 游戏状态控制器 —— 负责游戏状态机、暂停/恢复、时间冻结、应用生命周期
/// 
/// 【职责】
///   1. 维护游戏状态（None/Playing/Paused/GameOver）
///   2. 独立暂停标志 IsPaused（不依赖 CurrentState，确保暂停一定生效）
///   3. 暂停/恢复游戏（FreezeWorld/UnfreezeWorld 控制 timeScale）
///   4. 切后台自动保存 + 切回来自动弹出暂停面板
///   5. 退出游戏时保存进度
/// 
/// 【拆分来源】原 GameManager.cs 的状态管理、暂停逻辑、OnApplicationPause/OnApplicationQuit
/// </summary>
public class GameStateController : MonoBehaviour
{
    // 游戏状态
    public GameState CurrentState { get; private set; } = GameState.None;

    // 独立暂停标志（不依赖CurrentState，确保暂停一定生效，全局暂停检查用这个）
    public bool IsPaused { get; private set; } = false;

    /// <summary>设置游戏状态（外部一般不直接调用，用 StartNewGame/GameOver/RestartGame）</summary>
    public void SetGameState(GameState newState)
    {
        CurrentState = newState;
    }

    /// <summary>
    /// 新游戏开始时重置状态（不发事件，避免弹出暂停面板）
    /// 对应原 GameManager.StartNewGame 里的 IsPaused=false + CurrentState=Playing + UnfreezeWorld
    /// </summary>
    public void ResetForNewGame()
    {
        IsPaused = false;
        CurrentState = GameState.Playing;
        UnfreezeWorld();
    }

    /// <summary>
    /// 暂停游戏 —— 用独立的 IsPaused 标志，不依赖 CurrentState，确保暂停一定生效
    /// </summary>
    /// <param name="silent">true=静默暂停（不发事件，升级面板等内部暂停用，防止暂停面板也弹出来）</param>
    public void PauseGame(bool silent = false)
    {
        if (IsPaused) return; // 已经暂停了就不重复

        IsPaused = true;
        CurrentState = GameState.Paused;
        FreezeWorld(); // 复用停止逻辑

        // 静默暂停不发事件（升级面板用，防止暂停面板叠在一起）
        if (!silent)
        {
            EventBus.Instance.Publish(new GamePausedEventArgs() { isPaused = true });
        }

        Debug.Log("[GameStateController] ⏸️ 游戏已暂停（timeScale=0）" + (silent ? " [静默]" : ""));
    }

    /// <summary>恢复游戏</summary>
    /// <param name="silent">true=静默恢复（不发事件，升级面板关闭时用）</param>
    public void ResumeGame(bool silent = false)
    {
        if (!IsPaused) return; // 没暂停就不恢复

        IsPaused = false;
        CurrentState = GameState.Playing;
        UnfreezeWorld(); // 复用恢复逻辑

        // 静默恢复不发事件（升级面板关闭时用，防止 PopAll 把其他面板也关了）
        if (!silent)
        {
            EventBus.Instance.Publish(new GamePausedEventArgs() { isPaused = false });
        }

        Debug.Log("[GameStateController] ▶️ 游戏已恢复（timeScale=1）" + (silent ? " [静默]" : ""));
    }

    /// <summary>冻结游戏世界（暂停 / 游戏结束时调用）</summary>
    public void FreezeWorld()
    {
        Time.timeScale = 0f;
    }

    /// <summary>解冻游戏世界（恢复 / 重开 / 新开局时调用）</summary>
    public void UnfreezeWorld()
    {
        Time.timeScale = 1f;
    }

    /// <summary>
    /// 后台切回处理 —— 手机切后台再回来时自动暂停
    /// 防止玩家切后台接电话，回来发现角色已经死了
    /// 
    /// 真机流程：
    ///   切后台（pauseStatus=true）→ 保存进度，游戏还在跑但用户看不到
    ///   切回来（pauseStatus=false）→ 自动弹出暂停面板，让玩家点继续
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        // 只在游戏进行中处理，菜单/结算状态不需要
        if (CurrentState != GameState.Playing) return;

        if (pauseStatus)
        {
            // 切后台：保存当前局进度（手机被系统杀掉也能恢复）
            GameManager.Instance.SaveSystem.SaveCurrentRun();
            Debug.Log("[GameStateController] 切后台，已自动保存本局进度");
        }
        else
        {
            // 从后台回来：自动弹出暂停面板
            Debug.Log("[GameStateController] 应用从后台恢复，自动暂停游戏");
            PauseGame();
        }
    }

    private void OnApplicationQuit()
    {
        if (CurrentState == GameState.Playing)
        {
            GameManager.Instance.SaveSystem.SaveCurrentRun();
            Debug.Log("[GameStateController] 退出游戏，已保存本局进度");
        }
    }
}
