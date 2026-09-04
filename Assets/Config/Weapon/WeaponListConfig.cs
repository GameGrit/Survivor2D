using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "武器列表", menuName = "Game/WeaponListConfig")]
public class WeaponListConfig : ScriptableObject
{
    [Tooltip("项目所有武器配置集合，在这里把所有WeaponConfig拖进来")]
    public List<WeaponConfig> weaponConfigs = new List<WeaponConfig>();

    /// <summary> 根据weaponId查找武器配置，找不到返回null </summary>
    public WeaponConfig GetWeaponById(int weaponId)
    {
        foreach (var cfg in weaponConfigs)
        {
            if (cfg != null && cfg.weaponId == weaponId)
            {
                return cfg;
            }
        }
        return null;
    }

    /// <summary> 获取全部武器Id列表 </summary>
    public List<int> GetAllWeaponIds()
    {
        List<int> ids = new List<int>();
        foreach (var cfg in weaponConfigs)
        {
            if (cfg != null) ids.Add(cfg.weaponId);
        }
        return ids;
    }

    /// <summary> 判断是否存在该id武器 </summary>
    public bool HasWeapon(int weaponId)
    {
        return GetWeaponById(weaponId) != null;
    }
}
