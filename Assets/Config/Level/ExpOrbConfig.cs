using UnityEngine;

namespace Config
{
    /// <summary>
    /// 经验球等级枚举 —— 按经验值大小分档，不同档位视觉表现不同
    /// </summary>
    public enum ExpOrbGrade
    {
        Small,  // 小型经验球（低经验）
        Medium, // 中型经验球（中经验）
        Large   // 大型经验球（高经验/BOSS掉落）
    }

    /// <summary>
    /// 经验球全局配置 —— ScriptableObject 驱动
    /// 所有经验球的行为参数、视觉参数、分级阈值都从这里读
    /// 创建路径：Assets/Create/Configs/ExpOrbConfig
    /// </summary>
    [CreateAssetMenu(fileName = "ExpOrbConfig", menuName = "Configs/ExpOrbConfig")]
    public class ExpOrbConfig : ScriptableObject
    {
        [Header("===== 分级阈值 =====")]
        [Tooltip("经验值 >= 此值 → 中型球")]
        public int mediumThreshold = 6;

        [Tooltip("经验值 >= 此值 → 大型球")]
        public int largeThreshold = 16;

        [Header("===== 各等级视觉参数 =====")]
        [Tooltip("小型球颜色")]
        public Color smallColor = new Color(0.4f, 1f, 0.4f, 1f);

        [Tooltip("中型球颜色")]
        public Color mediumColor = new Color(0.4f, 0.6f, 1f, 1f);

        [Tooltip("大型球颜色")]
        public Color largeColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Tooltip("小型球缩放")]
        public float smallScale = 0.6f;

        [Tooltip("中型球缩放")]
        public float mediumScale = 0.85f;

        [Tooltip("大型球缩放")]
        public float largeScale = 1.2f;

        [Header("===== 掉落弹射参数 =====")]
        [Tooltip("掉落时初始弹射速度")]
        public float popSpeed = 3f;

        [Tooltip("弹射速度衰减系数（每秒乘多少，0.5=每秒减半）")]
        public float popDrag = 3f;

        [Tooltip("弹射持续时间（秒），之后进入待机")]
        public float popDuration = 0.4f;

        [Header("===== 吸附参数 =====")]
        [Tooltip("玩家进入此范围（世界单位）后，经验球开始被吸附")]
        public float attractRadius = 2.5f;

        [Tooltip("吸附初始速度")]
        public float attractStartSpeed = 2f;

        [Tooltip("吸附最大速度（越靠近玩家越快）")]
        public float attractMaxSpeed = 12f;

        [Tooltip("吸附加速度（每秒增加的速度）")]
        public float attractAcceleration = 20f;

        [Header("===== 拾取参数 =====")]
        [Tooltip("距离玩家小于此值即判定拾取")]
        public float pickUpDistance = 0.3f;

        [Header("===== 生命周期 =====")]
        [Tooltip("经验球最长存活时间（秒），超时自动回收防止场景堆积")]
        public float maxLifeTime = 60f;

        /// <summary>
        /// 根据经验值计算等级
        /// </summary>
        public ExpOrbGrade GetGrade(int expValue)
        {
            if (expValue >= largeThreshold) return ExpOrbGrade.Large;
            if (expValue >= mediumThreshold) return ExpOrbGrade.Medium;
            return ExpOrbGrade.Small;
        }

        /// <summary>
        /// 根据等级获取颜色
        /// </summary>
        public Color GetColor(ExpOrbGrade grade)
        {
            switch (grade)
            {
                case ExpOrbGrade.Large: return largeColor;
                case ExpOrbGrade.Medium: return mediumColor;
                default: return smallColor;
            }
        }

        /// <summary>
        /// 根据等级获取缩放
        /// </summary>
        public float GetScale(ExpOrbGrade grade)
        {
            switch (grade)
            {
                case ExpOrbGrade.Large: return largeScale;
                case ExpOrbGrade.Medium: return mediumScale;
                default: return smallScale;
            }
        }
    }
}
