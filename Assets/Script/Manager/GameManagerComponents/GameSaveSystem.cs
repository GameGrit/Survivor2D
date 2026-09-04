using UnityEngine;

/// <summary>
/// 游戏存档系统 —— 负责本局进度的保存与恢复
/// 
/// 【职责】
///   1. 保存当前局进度（退出/切后台/死亡时调用）
///   2. 从存档恢复本局进度（点"继续游戏"时调用）
///   3. 查询是否有可继续的局内存档
/// 
/// 【拆分来源】原 GameManager.cs 的 SaveCurrentRun/ContinueSavedRun/HasValidRunSave/GetPlayerHp/SetPlayerHp
/// </summary>
public class GameSaveSystem : MonoBehaviour
{
    /// <summary>保存当前局进度（退出游戏/切后台/死亡时调用）</summary>
    public void SaveCurrentRun()
    {
        // 允许 Playing（游戏中）和 Paused（暂停中）都保存，返回主菜单时是暂停状态
        if (GameManager.Instance.CurrentState != GameState.Playing
            && GameManager.Instance.CurrentState != GameState.Paused) return;

        try
        {
            var run = new RunSaveData
            {
                playerLevel = PlayerExp.Instance != null ? PlayerExp.Instance.level : 1,
                playerCurExp = PlayerExp.Instance != null ? PlayerExp.Instance.curExp : 0,
                maxHp = PlayerExp.Instance != null ? PlayerExp.Instance.maxHp : 100,
                attackDamage = PlayerExp.Instance != null ? PlayerExp.Instance.attackDamage : 10,
                moveSpeed = PlayerExp.Instance != null ? PlayerExp.Instance.moveSpeed : 3f,
                attackSpeedMultiplier = PlayerExp.Instance != null ? PlayerExp.Instance.attackSpeedMultiplier : 1f,
                bulletSpeed = PlayerExp.Instance != null ? PlayerExp.Instance.bulletSpeed : 8f,
                currentHp = GetPlayerHp(),
                currentWave = WaveSystem.Instance != null ? WaveSystem.Instance.currentWave : 1,

                hasValidRun = true,

                // ===== 升级卡片获得的属性也保存 =====
                lifeStealRate = PlayerExp.Instance != null ? PlayerExp.Instance.lifeStealRate : 0f,
                pickupRange = PlayerExp.Instance != null ? PlayerExp.Instance.pickupRange : 2f
            };
            SaveManager.Instance.Save(run);
            Debug.Log($"[GameSaveSystem] 本局进度已保存：等级{run.playerLevel}，波次{run.currentWave}，" +
                      $"吸血{run.lifeStealRate:P0}，拾取范围{run.pickupRange}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameSaveSystem] 保存本局进度失败：{ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>从存档恢复本局进度（点"继续游戏"时调用）</summary>
    public void ContinueSavedRun()
    {
        var run = SaveManager.Instance.Load<RunSaveData>();
        if (run == null || !run.hasValidRun)
        {
            Debug.LogWarning("[GameSaveSystem] 没有可继续的存档，开新局");
            GameManager.Instance.StartNewGame();
            return;
        }

        // 1. 恢复玩家属性
        PlayerExp.Instance.level = run.playerLevel;
        PlayerExp.Instance.curExp = run.playerCurExp;
        PlayerExp.Instance.maxHp = run.maxHp;
        PlayerExp.Instance.attackDamage = run.attackDamage;
        PlayerExp.Instance.moveSpeed = run.moveSpeed;
        PlayerExp.Instance.attackSpeedMultiplier = run.attackSpeedMultiplier;
        PlayerExp.Instance.bulletSpeed = run.bulletSpeed;

        // ===== 恢复升级卡片获得的属性 =====
        PlayerExp.Instance.lifeStealRate = run.lifeStealRate;
        PlayerExp.Instance.pickupRange = run.pickupRange;

        // 3. 恢复波次
        WaveSystem.Instance.SetWave(run.currentWave);

        // 3.5 重置怪物生成器（切场景后必须重新获取相机和玩家引用，否则 _mainCam==null 怪物不生成）
        MonsterSpawner.Instance.ResetSpawner();

        // 3.6 重置伤害数字管理器（切场景后旧画布销毁，必须重新找画布和重建对象池）
        if (DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.ResetManager();

        // 4. 恢复玩家对象位置和血量（继续游戏时回满血，避免死亡时保存的0血量导致一复活就死）
        GameManager.Instance.FlowController.ResetPlayerObject();
        SetPlayerHp(run.maxHp);

        // 5. 同步组件
        PlayerController controller = FindObjectOfType<PlayerController>();
        if (controller != null) controller.moveSpeed = run.moveSpeed;

        Player.PlayerAutoWeapon weapon = FindObjectOfType<Player.PlayerAutoWeapon>();
        if (weapon != null) weapon.RefreshAttackInterval();

        // 6. 进入游戏状态（ResetForNewGame 会重置 IsPaused + CurrentState + timeScale，不发事件）
        GameManager.Instance.StateController.ResetForNewGame();

        // 【关键修复】继续游戏后主动刷新HUD，确保等级/血量/层数等数字显示
        // 因为 ContinueSavedRun 可能在 PlayerHUD 订阅事件之前执行，事件会丢失
        PlayerHUD hud = FindObjectOfType<PlayerHUD>();
        if (hud != null)
        {
            hud.RefreshAll();
            Debug.Log("[GameSaveSystem] ContinueSavedRun 后主动刷新 HUD 完成");
        }
        else
        {
            Debug.LogWarning("[GameSaveSystem] ContinueSavedRun 时找不到 PlayerHUD，将在 Start 中自动刷新");
        }

        Debug.Log($"[GameSaveSystem] 已恢复存档：等级{run.playerLevel}，波次{run.currentWave}，" +
                  $"吸血{run.lifeStealRate:P0}，拾取范围{run.pickupRange}");
    }

    /// <summary>是否有可继续的局内存档</summary>
    public bool HasValidRunSave()
    {
        var run = SaveManager.Instance.Load<RunSaveData>();
        return run != null && run.hasValidRun;
    }

    private float GetPlayerHp()
    {
        var hp = FindObjectOfType<PlayerHealth>();
        return hp != null ? hp._currentHp : PlayerExp.Instance.maxHp;
    }

    private void SetPlayerHp(float hp)
    {
        var h = FindObjectOfType<PlayerHealth>();
        if (h != null) h.SetHp(hp);
    }
}
