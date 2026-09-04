using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UIManager;

public class StoreAndBagPanel : UIPanelBase
{
    public StartPanel startPanel;
    public TextMeshProUGUI coinTxt;
    public Button btnBack;
    [Header("商店列表父容器")]
    public Transform storeContentParent;   // 拖商店Grid

    [Header("背包列表父容器")]
    public Transform bagContentParent;     // 拖背包Grid
    private void OnEnable()
    {

        UpdateCoinText(CoinManager.Instance.CurrentCoin);
        EventBus.Instance.Subscribe<CoinChangedEventArgs>(OnCoinChanged);
        // 每次打开商店，让单例重新找UI并刷新
        BagAndStoreManager.Instance?.Reinit();
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<CoinChangedEventArgs>(OnCoinChanged);
    }

    private void Start()
    {
        coinTxt.text=CoinManager.Instance.CurrentCoin.ToString();
        btnBack.onClick.AddListener(() =>
        {
            gameObject.SetActive(false);
            startPanel.gameObject.SetActive(true);
        });
    }

    void OnCoinChanged(CoinChangedEventArgs e) => UpdateCoinText(e.newCoin);
    void UpdateCoinText(int coin) { if (coinTxt != null) coinTxt.text = coin.ToString(); }
}
