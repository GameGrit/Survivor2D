using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 伤害数字单体：上浮、渐隐、自动回池
/// </summary>
public class DamageNumber : MonoBehaviour
{
    [Header("组件引用")]
    public TextMeshProUGUI damageText;

    [Header("动画参数")]
    public float floatSpeed = 1f;       // 上浮速度
    public float fadeDuration = 0.8f;   // 渐隐时长
    public float scaleDuration = 0.15f; // 缩放弹出时长

    // 回池回调
    public System.Action<DamageNumber> OnRecycleCallback;

    private float _timer;
    private Color _originalColor;
    private Vector3 _originalScale;
    
    private void Awake()
    {
        if (damageText == null)
            damageText = GetComponent<TextMeshProUGUI>();

        _originalColor = damageText.color;
        _originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        _timer = 0;

        transform.localScale = _originalScale * 0.5f; // 从小变大弹出
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        // 1. 上浮
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // 2. 弹出缩放（前0.15秒从小变大）
        if (_timer < scaleDuration)
        {
            float t = _timer / scaleDuration;
            transform.localScale = Vector3.Lerp(_originalScale * 0.5f, _originalScale * 1.2f, t);
        }
        else if (_timer < scaleDuration * 2)
        {
            float t = (_timer - scaleDuration) / scaleDuration;
            transform.localScale = Vector3.Lerp(_originalScale * 1.2f, _originalScale, t);
        }

        // 3. 渐隐消失（最后fadeDuration秒）
        if (_timer > fadeDuration)
        {
            float fadeT = (_timer - fadeDuration) / 0.3f;
            Color c = damageText.color;
            c.a = Mathf.Lerp(1f, 0f, fadeT);
            damageText.color = c;
        }

        // 4. 到时间自动回池
        if (_timer >= fadeDuration + 0.3f)
        {
            RecycleSelf();
        }
    }

    /// <summary>
    /// 设置伤害数值和颜色
    /// </summary>
    public void SetDamage(float damage, bool isCritical = false, bool isPlayerHurt = true)
    {


        if (isPlayerHurt==true)
        {
            // 玩家受伤：红色（敌人打我）
            damageText.color = Color.red;
        }
        else if (isCritical)
        {
            // 暴击：黄色 + 放大
            damageText.color = Color.yellow;
            transform.localScale = _originalScale * 1.5f;
        }
        else
        {
            // 普通伤害：绿色（我打敌人）
            damageText.color = Color.green;
        }

        _originalColor = damageText.color;
                
        string txtStr= Mathf.RoundToInt(damage).ToString();// 取整显示
        damageText.text =$"-{txtStr}"; 
    }

    void RecycleSelf()
    {
        OnRecycleCallback?.Invoke(this);
    }
}
