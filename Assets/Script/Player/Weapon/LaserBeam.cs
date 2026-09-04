using System;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// 激光光束组件 —— 挂在光束贴图预制体上
    ///
    /// 【职责】
    ///   - 根据起点/终点自动计算位置、旋转、缩放，把贴图拉伸成一束光
    ///   - 持续极短时间后自动回收（模拟激光一闪而过）
    ///   - 通过对象池复用，避免频繁 Instantiate/Destroy
    ///
    /// 【预制体要求】
    ///   - 挂 SpriteRenderer，拖一张横向长条光束贴图（纯白/渐变色都行）
    ///   - Sprite 的 pivot 建议设为 Center（居中），draw mode = Simple
    ///   - 挂本脚本 LaserBeam
    ///   - 不需要 Collider（激光不靠碰撞检测伤害，靠射线）
    ///
    /// 【缩放原理】
    ///   用 sprite.bounds.size 拿到贴图原始世界尺寸，
    ///   scaleX = 目标长度 / 贴图原始宽度，scaleY = beamWidth / 贴图原始高度，
    ///   这样不管你贴图画的是 128x16 还是 256x32，都能正确拉伸。
    /// </summary>
    public class LaserBeam : MonoBehaviour, IPoolable
    {
        [Header("光束设置")]
        [Tooltip("光束持续时间（秒），激光一闪而过，建议0.05~0.15")]
        public float duration = 0.1f;

        [Tooltip("光束宽度（世界单位），也可被 WeaponConfig.laserWidth 覆盖")]
        public float beamWidth = 0.3f;

        private float _timer;
        private SpriteRenderer _sprite;

        /// <summary>回收回调，由发射方（LaserFire）注册</summary>
        public Action<GameObject> OnNeedRecycle;

        private void Awake()
        {
            _sprite = GetComponent<SpriteRenderer>();
            if (_sprite == null)
            {
                Debug.LogError($"[LaserBeam] {gameObject.name} 没有挂 SpriteRenderer！光束无法拉伸。", this);
            }
        }

        public void OnSpawn()
        {
            _timer = 0f;
            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            OnNeedRecycle = null;
        }

        /// <summary>
        /// 设置光束形态：从 start 拉伸到 end，自动算位置/旋转/缩放
        /// </summary>
        /// <param name="start">枪口世界坐标</param>
        /// <param name="end">光束终点世界坐标</param>
        public void SetBeam(Vector2 start, Vector2 end)
        {
            Vector2 dir = end - start;
            float length = dir.magnitude;

            // 长度为0就不显示（避免除零和奇怪的缩放）
            if (length < 0.001f)
            {
                gameObject.SetActive(false);
                return;
            }

            // 1. 位置：放在线段中点
            transform.position = (start + end) * 0.5f;

            // 2. 旋转：对齐飞行方向（0度=朝右，和Bullet一致）
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            // 3. 缩放：X轴拉到目标长度，Y轴设为光束宽度
            //    用贴图原始尺寸做归一化，兼容任意尺寸的光束贴图
            if (_sprite != null && _sprite.sprite != null)
            {
                Vector2 originalSize = _sprite.sprite.bounds.size;
                float scaleX = length / originalSize.x;
                float scaleY = beamWidth / originalSize.y;
                transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
            else
            {
                // 兜底：假设贴图是1x1单位
                transform.localScale = new Vector3(length, beamWidth, 1f);
            }
        }

        private void Update()
        {
            // 全局暂停：暂停时计时不推进，光束不会消失
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
                return;

            _timer += Time.deltaTime;
            if (_timer >= duration)
            {
                OnNeedRecycle?.Invoke(gameObject);
            }
        }
    }
}
