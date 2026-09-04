
using UnityEngine;

/// <summary>事件标记接口</summary>
/// <summary>获得经验事件</summary>
public class AddExpEventArgs : BaseEventArgs
{
    public int addValue;
    public int currentExp;
    public int nextLevelRequireExp;
}
/// <summary>金币变化事件（HUD和商店都监听）</summary>
public class CoinChangedEventArgs : BaseEventArgs
{
    public int oldCoin;
    public int newCoin;
    public int delta;
}

/// <summary>玩家升级事件，割草每升一级抛一次</summary>
public class PlayerLevelUpEventArgs : BaseEventArgs
{
    public int newLevel;
    public int remainExp;        //升级后剩余经验
    public int nextRequireExp;  //下一级需要经验
}

/// <summary>玩家血量变化事件（受伤/回血/加血上限时触发，HUD血条用）</summary>
public class PlayerHpChangedEventArgs : BaseEventArgs
{
    public float currentHp;   // 当前血量
    public float maxHp;       // 最大血量
    public float hpRatio;     // 血量比例（0~1），UI直接用
}

/// <summary>层数变化事件（进入新一层时触发，HUD右上角显示用）</summary>
public class WaveChangedEventArgs : BaseEventArgs
{
    public int newWave;           // 新层数
    public float difficultyFactor; // 当前难度系数
}

/// <summary>经验球被拾取事件（音效/特效/统计可监听）</summary>
public class ExpOrbPickedEventArgs : BaseEventArgs
{
    public int expValue;          // 拾取的经验值
    public Config.ExpOrbGrade grade; // 经验球等级
    public Vector3 position;      // 拾取位置
    public SfxType sfxType;

}

/// <summary>游戏结束事件（GameOver面板监听，展示结算数据）</summary>
public class GameOverEventArgs : BaseEventArgs
{
    public float surviveTime;     // 本局生存时间（秒）
    public int totalKills;        // 总击杀数
    public int playerLevel;       // 最终等级
    public int reachWave;         // 到达层数
}

/// <summary>怪物逻辑死亡事件（FSM/移动/碰撞已停止，等待播死亡动画）</summary>
public class MonsterDiedEventArgs : BaseEventArgs
{
    public MonsterBase monster;   // 死亡的怪物实例
}

/// <summary>怪物死亡动画播放完成事件（监听者负责回对象池）</summary>
public class MonsterDeathAnimationFinishedEventArgs : BaseEventArgs
{
    public MonsterBase monster;   // 死亡动画播完的怪物实例
}

/// <summary>子弹命中敌人事件（命中特效/震屏/连击统计可订阅）</summary>
public class BulletHitEventArgs : BaseEventArgs
{
    public Vector3 hitPosition;   // 命中点坐标
    public int damage;            // 本次伤害
    public MonsterBase monster;   // 被命中的怪物
}

/// <summary>玩家发射子弹事件（AudioManager 订阅播放射击音效）</summary>
public class BulletFiredEventArgs : BaseEventArgs
{
    public SfxType sfxType;  // 武器指定的射击音效类型，不同武器可不同
}

/// <summary>游戏暂停/恢复事件（PausePanel监听）</summary>
public class GamePausedEventArgs : BaseEventArgs
{
    public bool isPaused;  // true=暂停，false=恢复
}
