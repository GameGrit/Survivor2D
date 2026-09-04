using System;
using System.Collections.Generic;

/// <summary>
/// 永久进度（跨局保留）：金币、已购买商品、背包、装备
/// </summary>
[Serializable]
public class GameSaveData
{
    public int totalCoin = 0;
    public List<string> purchasedItemIds = new List<string>();
    public List<string> purchasedBagItemIds = new List<string>();
    public int equippedWeaponId = -1;
}

/// <summary>
/// 局内进度（继续游戏用）：等级、经验、属性、血量、波次
/// 【升级卡片获得的属性也保存在这里】吸血、拾取范围等
/// </summary>
[Serializable]
public class RunSaveData
{
    public int playerLevel = 1;
    public int playerCurExp = 0;
    public int maxHp = 100;
    public int attackDamage = 10;
    public float moveSpeed = 3f;
    public float attackSpeedMultiplier = 1f;
    public float bulletSpeed = 8f;
    public float currentHp = 100f;
    public int currentWave = 1;

    public bool hasValidRun = false;


    public float lifeStealRate = 0f;      // 吸血比例
    public float pickupRange = 2f;         // 拾取范围
}
