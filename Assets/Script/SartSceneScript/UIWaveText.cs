using UnityEngine;
[RequireComponent(typeof(RectTransform))]
public class UIWaveText : MonoBehaviour
{
    [Header("波浪配置")]
    [Tooltip("波浪相关的设置")]
    public float amplitude = 8f;     // 上下抖动幅度
    public float frequency = 3f;
    public float phaseOffset = 0.2f; // 每个字相位偏移，实现从左到右传递
    public RectTransform[] _charsRt;
    private Vector2[] _originAnchoredPos;
    private void Awake()
    {
        int childCount = transform.childCount;
        _charsRt = new RectTransform[childCount];
        _originAnchoredPos = new Vector2[childCount];
        for (int i = 0; i < childCount; i++)
        {
            _charsRt[i] = transform.GetChild(i).GetComponent<RectTransform>();
            _originAnchoredPos[i] = _charsRt[i].anchoredPosition;
        }
    }
    private void Update()
    {
        for (int i = 0; i < _charsRt.Length; i++)
        {
            // 每个字相位 = 时间 + i*偏移，越右边相位越延后，形成从左向右波浪
            float wave = Mathf.Sin(Time.time * frequency + i * phaseOffset);
            Vector2 pos = _originAnchoredPos[i];
            pos.y += wave * amplitude;
            _charsRt[i].anchoredPosition = pos;
        }
    }
}
