using UnityEngine;

public class EnhanceSystem : BaseMonoSingleton<EnhanceSystem>
{
    /// <summary>应用一个强化选项</summary>
    public void ApplyEnhance(EnhanceConfig config)
    {
        PlayerExp p = PlayerExp.Instance;
        Debug.Log($"[EnhanceSystem] ⚡ 应用强化：{config.showName}（类型={config.enhanceType}）");

        switch (config.enhanceType)
        {
            case EnhanceType.AddAttack:
                p.attackDamage += config.addAttackValue;
                Debug.Log($"[EnhanceSystem] 攻击力 → {p.attackDamage}");
                break;

            case EnhanceType.AddMaxHp:
                p.maxHp += config.addMaxHpValue;
                Debug.Log($"[EnhanceSystem] 血量上限 → {p.maxHp}（+{config.addMaxHpValue}）");
                // 加血量同时回等量血
                PlayerHealth health = FindPlayerHealth();
                if (health != null)
                {
                    health.Heal(config.addMaxHpValue);
                    Debug.Log($"[EnhanceSystem] 已同步回血 {config.addMaxHpValue}");
                }
                else
                {
                    Debug.LogError("[EnhanceSystem] ❌ 找不到 PlayerHealth，回血失败！请确保场景中有 Player 对象且 tag=Player");
                }
                break;

            case EnhanceType.AddMoveSpeed:
                p.moveSpeed += config.addMoveSpeedValue;
                // 同步给PlayerController
                PlayerController controller = FindPlayerComponent<PlayerController>();
                if (controller != null)
                {
                    controller.moveSpeed = p.moveSpeed;
                }
                break;
            case EnhanceType.AddAttackSpeed:
                // 攻速系数相乘（0.85=减少15%间隔）
                p.attackSpeedMultiplier *= config.attackCdScale;
                // 下限保护：系数最低0.2（最快5倍速），防止无限叠加导致0间隔
                p.attackSpeedMultiplier = Mathf.Max(0.2f, p.attackSpeedMultiplier);
                // 重新计算武器实际攻击间隔
                Player.PlayerAutoWeapon weapon = FindPlayerComponent<Player.PlayerAutoWeapon>();
                if (weapon != null)
                {
                    weapon.RefreshAttackInterval();
                }
                Debug.Log($"[EnhanceSystem] 攻速系数 → {p.attackSpeedMultiplier:F2}");
                break;

            case EnhanceType.AddBulletSpeed:
                p.bulletSpeed += config.addBulletSpeedValue;
                break;

            case EnhanceType.AddPickupRange:
                p.pickupRange += config.pickupRangeValue;
                Debug.Log($"[EnhanceSystem] 拾取范围 → {p.pickupRange}");
                break;

            case EnhanceType.AddLifeSteal:
                p.lifeStealRate += config.lifeStealRate;
                Debug.Log($"[EnhanceSystem] 吸血 → {p.lifeStealRate:P0}");
                break;
        }

        // 抛属性变化事件，UI刷新
        EventBus.Instance.Publish(new PlayerStatsChangedEventArgs()
        {
            level = p.level,
            maxHp = p.maxHp,
            attackDamage = p.attackDamage,
            moveSpeed = p.moveSpeed,
            attackSpeedMultiplier = p.attackSpeedMultiplier,
            bulletSpeed = p.bulletSpeed
        });
    }

    /// <summary>
    /// 查找玩家身上的组件（优先用 FindObjectOfType，兼容玩家标签未设置的情况）
    /// </summary>
    private T FindPlayerComponent<T>() where T : Component
    {
        // 先按 tag 找（快）
        GameObject playerGo = GameObject.FindWithTag("Player");
        if (playerGo != null)
        {
            T comp = playerGo.GetComponent<T>();
            if (comp != null) return comp;
        }
        // tag 找不到就全场景找（保底）
        return FindObjectOfType<T>();
    }

    private PlayerHealth FindPlayerHealth()
    {
        return FindPlayerComponent<PlayerHealth>();
    }
}
