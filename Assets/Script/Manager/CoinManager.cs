using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinManager : BaseMonoSingleton<CoinManager>
{
    public int CurrentCoin { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        LoadFromSave();
    }

    /// <summary>加金币（拾取经验球时调用）</summary>
    public void AddCoin(int amount)
    {
        if (amount <= 0) return;
        int old = CurrentCoin;
        CurrentCoin += amount;
        SaveToSave();
        EventBus.Instance.Publish(new CoinChangedEventArgs { oldCoin = old, newCoin = CurrentCoin, delta = amount });

    }
    public bool SpendCoin(int amount)
    {
        if (amount <= 0) return false;
        int old = CurrentCoin;
        CurrentCoin -= amount;

        if (CurrentCoin < 0)
        {
            CurrentCoin = old;
            return false;
        }
        SaveToSave();
        EventBus.Instance.Publish(new CoinChangedEventArgs { oldCoin = old, newCoin = CurrentCoin, delta = -amount });
        return true;
    }
    public bool CanAfford(int amount) => CurrentCoin >= amount;
    public void LoadFromSave()
    {
        var save = SaveManager.Instance.Load<GameSaveData>();
        if (save != null) CurrentCoin = save.totalCoin;
    }

    public void SaveToSave()
    {
        var save = SaveManager.Instance.Load<GameSaveData>() ?? new GameSaveData();
        save.totalCoin = CurrentCoin;
        SaveManager.Instance.Save(save);
    }

}
