using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName ="BagItem",menuName ="Store/Bag")]
public class BagItemConfig : ScriptableObject
{
    [Header("基础信息")]
    public string bagItemId;
    public string bagItemName;

    [Header("显示")]
    public Sprite spriteItem;

    [Header("武器关联")]
    [Tooltip("如果这个背包物品是武器，填对应的武器ID（和WeaponConfig.weaponId一致）。非武器填-1")]
    public int weaponId = -1;

    [Header("高级物品标识")]
    public bool isAdvanced = false;

    [Header("关联物体（可选，用于特殊效果）")]
    public GameObject obj;
}
