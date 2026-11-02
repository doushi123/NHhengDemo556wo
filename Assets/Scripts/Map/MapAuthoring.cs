using UnityEngine;

/// <summary>
/// 指定地图碰撞层和边界，只供地图制作与烘焙使用。
/// </summary>
public sealed class MapAuthoring : MonoBehaviour
{
    [Min(1)] [SerializeField] private int mapID = 1;
    [Tooltip("选择地面和静态障碍所在层，例如 BattleGround 与 BattleBlocker。无需调整地图父子层级。")]
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private Vector3 boundsSize = new Vector3(40f, 10f, 40f);

    public int MapID => mapID;
    public LayerMask CollisionLayers => collisionLayers;
    public Bounds MapBounds => new Bounds(transform.position, boundsSize);

    /// <summary>
    /// 选中地图范围对象时显示导出区域。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, boundsSize);
    }
}
