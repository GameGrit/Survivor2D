using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartPanel : MonoBehaviour
{
    public Button btnStore;
    public Button btnStart;
    public Button btnSetting;
    public Button btnStop;
    public GameObject SettingPanel;
    public GameObject StorePanel;
    // Start is called before the first frame update
    void Start()
    {
        StorePanel.SetActive(false);
        btnStop.onClick.AddListener(() =>
        {
            Application.Quit();
        });

        btnStore.onClick.AddListener(() => { 
            StorePanel.SetActive(true);
            gameObject.SetActive(false);
        });
        SettingPanel.gameObject.SetActive(false);
        // 播放主菜单BGM
        AudioManager audioManager = GameObject.FindWithTag("AudioManager").GetComponent<AudioManager>();
        audioManager.PlayBgm(BgmType.MainMenu);

        btnStart.onClick.AddListener(() =>
        {
            // 【Addressables 改造】场景已打包到 Remote_Scenes 组，必须用 Addressables 异步加载
            // 不能再用 SceneManager.LoadScene("GameScene")，因为场景不在 Build Settings 里
            audioManager.PlayBgm(BgmType.Battle);
            AddressablesManager.Instance.LoadSceneAsync("GameScene", null, error =>
            {
                Debug.LogError("[StartPanel] 加载 GameScene 失败：" + error);
            });
        });
        btnSetting.onClick.AddListener(() =>
        {
            // 打开设置面板时，隐藏开始面板
            gameObject.SetActive(false);
            SettingPanel.gameObject.SetActive(true);
        });
    }

}
