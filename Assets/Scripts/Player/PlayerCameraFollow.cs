using UnityEngine;

/// <summary>
/// 3D 客户端中的英雄跟随摄像机。
/// 鼠标控制镜头绕英雄旋转，摄像机保持固定距离跟随目标。
/// </summary>
public sealed class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -3f);
    /// <summary>
    /// 镜头注视角色脚底上方多高的位置
    /// </summary>
    [SerializeField] private float lookHeight = 1f;
    /// <summary>
    /// 上下旋转的角度范围
    /// </summary>
    [SerializeField] private float minPitch = -55f;
    [SerializeField] private float maxPitch = 30f;
    /// <summary>
    /// 当前累计的水平旋转角度
    /// </summary>
    private float yaw;
    /// <summary>
    /// 当前累计的竖直旋转角度
    /// </summary>
    private float pitch;

    private void Awake()
    {
        yaw = transform.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }
        //左右移动鼠标，累积水平角度
        yaw += Input.GetAxisRaw("Mouse X");
        pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y"), minPitch, maxPitch);

        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        //算出注视点
        Vector3 orbitOffset = offset - Vector3.up * lookHeight;
        transform.position = lookPoint + Quaternion.Euler(pitch, yaw, 0f) * orbitOffset;
        transform.LookAt(lookPoint);
    }

}
