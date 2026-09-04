using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStartup : BaseMonoSingleton<GameStartup>
{
    /// <summary>
    /// 游戏启动入口，框架初始化
    /// </summary>
    public Transform uiRoot;
    public Transform poolRoot;

    [Header("玩家初始属性")]
    public int startMaxHp = 100;
    public int startAttack = 10;
    public float startMoveSpeed = 3f;

    public float startAttackSpeedMultiplier = 1f;

    public float startBulletSpeed = 1f;

    public void Awake()
    {


        //DontDestroyOnLoad(gameObject);

        //UIManager赋值UI根节点
        UIManager.Instance.uiRoot = uiRoot;

        Debug.Log("✅底层框架初始化完成");
    }

    void Start()
    {
        if (GameManager.Instance.HasValidRunSave())
        {
            GameManager.Instance.ContinueSavedRun();
            return;  // 读档成功，下面的新开逻辑不执行了
        }
        // 初始化玩家属性（这一步之前漏掉了！导致 moveSpeed=0、maxHp=0）
        PlayerExp.Instance.ResetPlayerForNewGame(
            startMaxHp,
            startAttack,
            startMoveSpeed,
            startAttackSpeedMultiplier,
            startBulletSpeed
        );

        // 重置玩家血量
        PlayerHealth health = GameObject.FindWithTag("Player")?.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.ResetHp();
        }

        // 同步移速给 PlayerController
        PlayerController controller = GameObject.FindWithTag("Player")?.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.moveSpeed = PlayerExp.Instance.moveSpeed;
        }
        
        // 启动游戏
        GameManager.Instance.StartNewGame();

        // 初始化层数系统
        WaveSystem.Instance.ResetSystem();

        Debug.Log($"✅游戏启动！初始血量={startMaxHp}，移速={startMoveSpeed}，攻击力={startAttack}");
    }
}
