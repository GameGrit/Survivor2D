using UnityEngine;

/// <summary>
/// 武器瞄准方向控制：根据摇杆输入旋转武器，并处理左右翻面。
///
/// 【设计原则】
///   - 子弹开火方向 = 玩家摇杆输入方向（直接取，不依赖武器旋转反推，避免 spriteFacingLeft 配错导致方向反转）
///   - 武器旋转/翻面 = 纯视觉表现，同时影响 FirePoint 子物体的世界位置
///   - spriteFacingLeft 必须和精灵图实际枪口朝向一致，否则 FirePoint 会出现在错误的一侧
/// </summary>
public class WeaponDir : MonoBehaviour
{
    [Header("引用")]
    public PlayerController charController;

    [Tooltip("武器自己的SpriteRenderer（不是角色的！）")]
    public SpriteRenderer weaponSprite;

    [Tooltip("枪口位置空物体（武器的子节点，放在枪口尖端），不填则用武器中心")]
    public Transform firePoint;

    [Header("设置")]
    [Tooltip("武器原始朝向偏移：枪口朝右填0，朝上填-90，朝下填90")]
    public float angleOffset = 0f;

    [Tooltip("精灵图枪口是否朝左（如果你的武器图片枪口朝左画的，勾选这个；朝右则不勾）")]
    public bool spriteFacingLeft = false;

    [Header("调试")]
    [Tooltip("勾选后在Console打印开火位置和方向，排查子弹从奇怪地方发出的问题")]
    public bool debugLog = false;

    private bool _isFacingLeft;
    private Vector2 _lastAimDir = Vector2.right; // 记录最后一次有效方向，无输入时兜底

    // FirePoint 原始本地坐标缓存——flipX 只翻 SpriteRenderer 图片，不翻子物体 Transform，
    // 往左走时需要手动把 FirePoint.localPosition.x 取反，否则子弹从错误的一侧发出
    private Vector3 _originalFirePointLocalPos;
    private bool _firePointPosCached;

    void Awake()
    {
        // 只初始化引用，不设旋转——WeaponManager.SwitchWeapon 会在 Instantiate 后
        // 立即覆盖 localRotation=identity，这里设了也会被冲掉
        InitReferences();
    }

    void Start()
    {
        InitReferences();
    }

    /// <summary>初始化 charController 和 weaponSprite 引用（幂等，可重复调用）</summary>
    void InitReferences()
    {
        if (charController == null)
        {
            charController = GetComponentInParent<PlayerController>();
        }
        if (weaponSprite == null)
        {
            weaponSprite = GetComponent<SpriteRenderer>();
        }
    }

    /// <summary>
    /// 外部强制刷新朝向（WeaponFireBase.Init 时调用，确保切武器后第一帧就正确）
    /// </summary>
    public void RefreshAim()
    {
        InitReferences();
        ApplyAimRotation();
    }

    void Update()
    {
        if (charController == null)
        {
            charController = GetComponentInParent<PlayerController>();
            if (charController == null) return;
        }

        ApplyAimRotation();
    }

    /// <summary>根据玩家输入方向更新武器朝向、翻面和旋转（纯视觉）</summary>
    void ApplyAimRotation()
    {
        if (charController == null) return;

        Vector2 dir = charController.dir;
        // 摇杆没有输入就保持当前朝向，不做处理
        if (dir.magnitude < 0.01f) return;

        _lastAimDir = dir.normalized;

        // 1. 计算瞄准角度（世界空间，0度=朝右，90度=朝上）
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 2. 根据输入X分量决定朝向哪边
        _isFacingLeft = dir.x < 0f;

        // 3. 武器图片翻面（只翻武器自己，不影响旋转坐标系）
        if (weaponSprite != null)
        {
            weaponSprite.flipX = _isFacingLeft;
        }

        // 3.5 【关键】同步翻转 FirePoint 子物体位置
        //     flipX 只翻转 SpriteRenderer 图片，不影响子物体 Transform。
        //     往左走时图片被翻到左边，但 FirePoint 还在右边 → 子弹从错误一侧发出。
        //     解决：往左走时把 FirePoint.localPosition.x 取反，让它和图片一起翻到左边。
        if (firePoint != null)
        {
            if (!_firePointPosCached)
            {
                _originalFirePointLocalPos = firePoint.localPosition;
                _firePointPosCached = true;
            }
            Vector3 fpPos = _originalFirePointLocalPos;
            if (_isFacingLeft)
            {
                fpPos.x = -fpPos.x;
            }
            firePoint.localPosition = fpPos;
        }

        // 4. 角色也同步翻面（让角色面朝和武器一致）
        SpriteRenderer charSprite = charController.GetComponent<SpriteRenderer>();
        if (charSprite != null)
        {
            charSprite.flipX = _isFacingLeft;
        }

        // 5. 旋转角度与翻面的配合
        //    精灵图朝右（spriteFacingLeft=false）：
        //      朝右(flipX=false): rotation=angle
        //      朝左(flipX=true):  rotation=angle-180（翻转后枪口朝左，减180补偿）
        //    精灵图朝左（spriteFacingLeft=true）：在上面基础上整体+180
        float baseAngle = _isFacingLeft ? angle - 180f : angle;
        float finalAngle = spriteFacingLeft ? baseAngle + 180f : baseAngle;
        transform.rotation = Quaternion.Euler(0, 0, finalAngle + angleOffset);
    }

    /// <summary>
    /// 获取枪口世界坐标（子弹生成位置）
    /// </summary>
    public Vector2 GetFirePointPosition()
    {
        Vector2 pos = firePoint != null ? firePoint.position : (Vector2)transform.position;

        if (debugLog)
        {
            Debug.Log($"[WeaponDir] 开火位置: {pos}, 武器中心: {transform.position}, " +
                      $"firePoint={(firePoint != null ? firePoint.name : "null(用武器中心)")}, " +
                      $"rotationZ={transform.rotation.eulerAngles.z:F1}°, spriteFacingLeft={spriteFacingLeft}");
        }

        return pos;
    }

    /// <summary>
    /// 获取开火方向 —— 直接取玩家摇杆输入方向，不依赖武器旋转反推
    /// 【为什么这样设计】
    ///   原来用 transform.right + _isFacingLeft + spriteFacingLeft 三重取反，
    ///   只要 spriteFacingLeft 和精灵实际朝向不匹配，方向就会反转，子弹打不到怪。
    ///   现在直接用输入方向，子弹永远朝摇杆方向飞，和武器旋转解耦。
    /// </summary>
    public Vector2 GetFireDirection()
    {
        Vector2 dir;

        // 优先用当前帧输入方向
        if (charController != null && charController.dir.sqrMagnitude > 0.01f)
        {
            dir = charController.dir.normalized;
            _lastAimDir = dir;
        }
        else
        {
            // 无输入时用最后一次有效方向兜底
            dir = _lastAimDir;
        }

        if (debugLog)
        {
            Debug.Log($"[WeaponDir] 开火方向: {dir}, 输入dir={(charController != null ? charController.dir.ToString() : "null")}");
        }

        return dir;
    }
}
