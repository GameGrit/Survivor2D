using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 自由方向虚拟摇杆 —— 纯360°自由方向，无方向吸附
/// 
/// 【与第三方 SimpleJoystick 的区别】
///   - 移除了 directionSnaps 方向吸附逻辑，永远输出真实拖拽方向
///   - 代码更精简，只保留核心功能，便于维护和扩展
///   - 接口兼容：同样提供 InputDirection 属性，PlayerController 可无缝替换
/// 
/// 【使用方法】
///   1. 在 Canvas 下建一个 Image 作为摇杆底座（joystickBase）
///   2. 底座下建一个小 Image 作为摇杆手柄（handle）
///   3. 把本脚本挂在底座上，拖好引用
///   4. PlayerController 的 joystickController 字段改拖这个脚本
/// 
/// 【三种模式】
///   - Static：固定位置，必须在底座范围内按下才生效
///   - Dynamic：按下时底座跟随手指移动，手柄在范围内拖动
///   - Floating：按下时底座出现在手指位置，松开消失
/// </summary>
public class FreeJoystickController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public enum JoystickMode
    {
        Static,    // 固定位置
        Dynamic,   // 动态跟随
        Floating   // 浮动出现
    }

    [Header("引用")]
    [Tooltip("摇杆底座（背景图）")]
    public RectTransform joystickBase;

    [Tooltip("摇杆手柄（可拖动的小圆）")]
    public RectTransform handle;

    [Header("设置")]
    [Tooltip("摇杆模式")]
    public JoystickMode mode = JoystickMode.Dynamic;

    [Tooltip("手柄可拖动半径（像素）")]
    public float joystickRange = 55f;

    [Tooltip("死区（0~1），小于此值的输入视为零，防止误触")]
    [Range(0f, 1f)] public float deadZone = 0.1f;

    [Tooltip("松开时手柄是否回弹到中心")]
    public bool snapHandleBack = true;

    /// <summary>
    /// 当前输入方向（x、y 范围 -1~1，magnitude <= 1）
    /// 自由方向：任意角度都能输出，不做8方向/4方向吸附
    /// </summary>
    public Vector2 InputDirection => _inputDirection;

    // 事件（可选订阅）
    public event Action OnTouchPressed;
    public event Action OnTouchRemoved;
    public event Action OnDirectionChanged;

    private Vector2 _inputDirection = Vector2.zero;
    private Vector2 _baseStartPos;
    private Canvas _parentCanvas;
    private bool _dragStartedInside = false;

    private void Awake()
    {
        if (joystickBase == null)
        {
            Debug.LogError($"❌ {gameObject.name}: joystickBase 未赋值！", this);
            enabled = false;
            return;
        }
        if (handle == null)
        {
            Debug.LogError($"❌ {gameObject.name}: handle 未赋值！", this);
            enabled = false;
            return;
        }

        _baseStartPos = joystickBase.anchoredPosition;
        _parentCanvas = GetComponentInParent<Canvas>();

        // 非静态模式初始隐藏底座
        if (mode != JoystickMode.Static)
        {
            joystickBase.gameObject.SetActive(false);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnTouchPressed?.Invoke();
        joystickBase.gameObject.SetActive(true);

        // 浮动模式：底座出现在手指按下位置
        if (mode == JoystickMode.Floating)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentCanvas.transform as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 newPos))
            {
                joystickBase.anchoredPosition = newPos;
            }
        }

        // 动态/浮动模式：底座跟随手指
        if (mode != JoystickMode.Static)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    joystickBase.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 touchPoint))
            {
                if (snapHandleBack)
                {
                    joystickBase.anchoredPosition = touchPoint;
                }
                else
                {
                    joystickBase.anchoredPosition = touchPoint - (_inputDirection * joystickRange);
                    handle.anchoredPosition = _inputDirection * joystickRange;
                }
            }
        }

        // 记录是否在底座范围内按下（静态模式用）
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBase,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            _dragStartedInside = localPoint.magnitude <= joystickRange;
        }

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 静态模式：必须在底座范围内按下才响应
        if (mode == JoystickMode.Static && !_dragStartedInside)
            return;

        // 将屏幕坐标转为底座本地坐标
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBase,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        // 限制手柄在半径范围内
        Vector2 clamped = Vector2.ClampMagnitude(localPoint, joystickRange);
        handle.anchoredPosition = clamped;

        // 计算归一化方向（自由方向：直接用真实坐标，不做角度吸附）
        Vector2 rawInput = clamped / joystickRange;
        Vector2 newDir = rawInput.magnitude < deadZone ? Vector2.zero : rawInput;

        // 方向变化时触发事件
        if (newDir != _inputDirection)
        {
            _inputDirection = newDir;
            if (_inputDirection != Vector2.zero)
            {
                OnDirectionChanged?.Invoke();
            }
        }

        // 动态模式：手指拖出范围时，底座跟随手指移动
        if (mode == JoystickMode.Dynamic && localPoint.magnitude > joystickRange)
        {
            Vector2 offset = localPoint.normalized * (localPoint.magnitude - joystickRange);
            joystickBase.anchoredPosition += offset;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        OnTouchRemoved?.Invoke();

        // 手柄回弹
        if (snapHandleBack)
        {
            handle.anchoredPosition = Vector2.zero;
            _inputDirection = Vector2.zero;
        }

        // 动态/浮动模式：底座复位并隐藏
        if (mode == JoystickMode.Floating || mode == JoystickMode.Dynamic)
        {
            joystickBase.anchoredPosition = _baseStartPos;
        }

        if (mode != JoystickMode.Static)
        {
            joystickBase.gameObject.SetActive(false);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Scene 视图中绘制摇杆范围辅助线
    /// 【修正】用 GetWorldCorners 获取底座真实世界坐标和半径，确保与白色底座完美吻合
    ///   - 白色虚线圆 = 摇杆底座实际边界
    ///   - 绿色半透明圆 = 手柄实际可拖动范围（joystickRange）
    /// </summary>
    private void OnDrawGizmos()
    {
        if (joystickBase == null) return;

        // 获取底座四个角的世界坐标，计算真实中心和半径
        Vector3[] corners = new Vector3[4];
        joystickBase.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) / 2f; // 对角线中点 = 中心
        float baseWorldRadius = Vector3.Distance(corners[0], corners[2]) / 2f; // 外接圆半径

        // 可拖动范围的世界半径（像素 × 缩放）
        float handleWorldRadius = joystickRange * joystickBase.lossyScale.x;

        // 外圈：白色虚线 = 底座边界
        UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.6f);
        UnityEditor.Handles.DrawWireDisc(worldCenter, Vector3.forward, baseWorldRadius);

        // 内圈：绿色半透明 = 手柄可拖动范围
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(worldCenter, Vector3.forward, handleWorldRadius);
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.7f);
        UnityEditor.Handles.DrawWireDisc(worldCenter, Vector3.forward, handleWorldRadius);

        // 如果可拖动范围超出底座，画红色警告圈
        if (handleWorldRadius > baseWorldRadius)
        {
            UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(worldCenter, Vector3.forward, handleWorldRadius);
        }
    }
#endif
}
