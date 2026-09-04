using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollowWithBounds : MonoBehaviour
{
    [Header("要跟随的玩家")]
    public Transform player;

    [Header("背景Sprite（你的地牢背景图）")]
    public SpriteRenderer background;

    [Header("平滑跟随速度")]
    public float smoothSpeed = 5f;

    private Camera cam;
    //相机允许移动的边界
    private float minX;
    private float maxX;
    private float minY;
    private float maxY;

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        CalculateCameraBounds();
    }

    /// <summary>
    /// 根据背景大小，计算相机不能超出的边界
    /// </summary>
    void CalculateCameraBounds()
    {
        //背景世界坐标边界
        Bounds bgBounds = background.bounds;

        //相机半宽、半高（正交相机视口一半大小）
        float camHalfHeight = cam.orthographicSize;
        float camHalfWidth = camHalfHeight * cam.aspect;

        // 计算相机允许的极限位置：
        // 相机的边缘不能超过背景边缘，所以要减去相机自身的半尺寸
        minX = bgBounds.min.x + camHalfWidth;
        maxX = bgBounds.max.x - camHalfWidth;

        minY = bgBounds.min.y + camHalfHeight;
        maxY = bgBounds.max.y - camHalfHeight;
    }


    void LateUpdate()
    {
        if (player == null) return;

        //目标位置：跟着玩家，只取XY，Z保持相机自己的值不动
        Vector3 targetPos = new Vector3(player.position.x, player.position.y, transform.position.z);

        //平滑插值跟随
        Vector3 smoothPos = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);

        // =========核心！钳位边界，超出就锁住=========
        smoothPos.x = Mathf.Clamp(smoothPos.x, minX, maxX);
        smoothPos.y = Mathf.Clamp(smoothPos.y, minY, maxY);

        transform.position = smoothPos;
    }

    //如果你修改背景大小，可以在外部调用这个重新计算边界
    public void RefreshBounds()
    {
        CalculateCameraBounds();
    }
}
