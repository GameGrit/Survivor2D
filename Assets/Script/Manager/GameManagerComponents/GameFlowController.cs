using UnityEngine;

/// <summary>
/// 游戏流程控制器 —— 负责游戏开始/结束/重开流程、本局统计、玩家重置
/// 
/// 【职责】
///   1. 本局统计数据（生存时间、总击杀数）
///   2. 游戏流程（开始新局、游戏结束、重新开始）
///   3. 玩家对象重置（位置、血量、组件同步）
///   4. 缓存 GameStartup 的初始属性配置
/// 
/// 【拆分来源】原 GameManager.cs 的 StartNewGame/GameOver/RestartGame/ResetPlayerObject/统计数据/GameStartupCached
/// </summary>
public class GameFlowController : MonoBehaviour
{
    [Tooltip("玩家初始出生点（由 GameManager 同步，不在此直接配置）")]
    [HideInInspector] public Vector3 playerSpawnPosition = Vector3.zero;

    // 本局统计
    public float SurviveGameTime { get; private set; }
    public int TotalKills { get; private set; }

    private void Update()
    {
        if (GameManager.Instance.CurrentState == GameState.Playing)
        {
            SurviveGameTime += Time.deltaTime;
        }
    }

    /// <summary>怪物死亡时调用，累计击杀数</summary>
    public void AddKill()
    {
        TotalKills++;
    }

    /// <summary>开始新一局游戏（首次启动用）</summary>
    public void StartNewGame()
    {
        ResetGameData();
        // ResetForNewGame 会设置 IsPaused=false + CurrentState=Playing + UnfreezeWorld，不发事件
        GameManager.Instance.StateController.ResetForNewGame();
        MonsterSpawner.Instance.ResetSpawner();
    }

    /// <summary>游戏结束 —— 玩家死亡时调用</summary>
    public void GameOver()
    {
        if (GameManager.Instance.CurrentState == GameState.GameOver) return; // 防止重复触发

        // 【关键修改】死亡时先保存当前进度，而不是删掉
        // 这样返回主菜单再点开始游戏，会从死亡前的等级/经验/层数继续
        GameManager.Instance.SaveSystem.SaveCurrentRun();

        GameManager.Instance.SetGameState(GameState.GameOver);
        GameManager.Instance.StateController.FreezeWorld(); // 暂停游戏世界（复用停止逻辑）

        // 隐藏场上所有伤害数字，避免飘字盖住结算面板
        if (DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.HideAllActive();

        // 发布游戏结束事件，GameOverPanel 监听后弹出结算
        EventBus.Instance.Publish(new GameOverEventArgs()
        {
            surviveTime = SurviveGameTime,
            totalKills = TotalKills,
            playerLevel = PlayerExp.Instance.level,
            reachWave = WaveSystem.Instance.currentWave
        });
    }

    /// <summary>重新开始 —— 从GameOver状态回到Playing</summary>
    public void RestartGame()
    {
        // 1. 恢复时间流速
        GameManager.Instance.StateController.UnfreezeWorld();

        // 2. 清空对象池（场上所有怪物、子弹、经验球全部回收）
        PoolManager.Instance.ClearAll();

        // 3. 重置游戏数据和状态
        ResetGameData();
        GameManager.Instance.SetGameState(GameState.Playing);

        // 4. 重置各管理器
        MonsterSpawner.Instance.ResetSpawner();
        WaveSystem.Instance.ResetSystem();

        // 5. 重置玩家属性
        PlayerExp.Instance.ResetPlayerForNewGame(
            GameStartupCached.startMaxHp,
            GameStartupCached.startAttack,
            GameStartupCached.startMoveSpeed,
            GameStartupCached.startAttackSpeedMultiplier,
            GameStartupCached.startBulletSpeed
        );

        // 6. 重置玩家对象（位置、血量、激活、组件同步）
        ResetPlayerObject();

        // 7. 刷新经验球管理器的玩家引用（玩家对象可能被重新激活）
        if (Manager.ExpOrbManager.Instance != null)
        {
            Manager.ExpOrbManager.Instance.RefreshPlayerCache();
        }

        Debug.Log("[GameFlowController] 🔄 游戏重新开始！");
    }

    /// <summary>重置本局数据</summary>
    private void ResetGameData()
    {
        SurviveGameTime = 0f;
        TotalKills = 0;
    }

    /// <summary>重置玩家对象到初始状态</summary>
    public void ResetPlayerObject()
    {
        GameObject playerGo = GameObject.FindWithTag("Player");
        if (playerGo == null)
        {
            Debug.LogError("[GameFlowController] 重新开始时找不到 Player 对象！");
            return;
        }

        // 重新激活（死亡时被 SetActive(false)）
        playerGo.SetActive(true);

        // 重置位置
        playerGo.transform.position = playerSpawnPosition;

        // 重置血量
        PlayerHealth health = playerGo.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.ResetHp();
        }

        // 同步移速
        PlayerController controller = playerGo.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.moveSpeed = PlayerExp.Instance.moveSpeed;
        }

        // 同步攻击间隔
        Player.PlayerAutoWeapon weapon = playerGo.GetComponent<Player.PlayerAutoWeapon>();
        if (weapon != null)
        {
            weapon.RefreshAttackInterval();
        }
    }

    // 缓存 GameStartup 的初始属性配置（避免每次重启都 Find）
    private static class GameStartupCached
    {
        public static int startMaxHp = 100;
        public static int startAttack = 10;
        public static float startMoveSpeed = 3f;
        public static float startAttackSpeedMultiplier = 1f;
        public static float startBulletSpeed = 1f;
        private static bool _initialized;

        /// <summary>从场景中的 GameStartup 读取初始配置，只读一次</summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            GameStartup startup = Object.FindObjectOfType<GameStartup>();
            if (startup != null)
            {
                startMaxHp = startup.startMaxHp;
                startAttack = startup.startAttack;
                startMoveSpeed = startup.startMoveSpeed;
                startAttackSpeedMultiplier = startup.startAttackSpeedMultiplier;
                startBulletSpeed = startup.startBulletSpeed;
                _initialized = true;
            }
        }
    }

    private void Awake()
    {
        GameStartupCached.EnsureInitialized();
    }
}
