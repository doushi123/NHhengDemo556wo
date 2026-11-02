using UnityEngine;

/// <summary>
/// 指定地图碰撞层、边界和手动出生点，只供地图制作与烘焙使用。
/// </summary>
public sealed class MapAuthoring : MonoBehaviour
{
    [Min(1)] [SerializeField] private int mapID = 1;
    [Tooltip("选择地面和静态障碍所在层，例如 BattleGround 与 BattleBlocker。无需调整地图父子层级。")]
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private Vector3 boundsSize = new Vector3(40f, 10f, 40f);

    /// <summary>阵营 A 的席位 0 出生点。</summary>
    [Header("手动出生点（位置代表角色脚底）")]
    [SerializeField] private Transform spawn0;

    /// <summary>阵营 A 的席位 1 出生点。</summary>
    [SerializeField] private Transform spawn1;

    /// <summary>阵营 B 的席位 2 出生点。</summary>
    [SerializeField] private Transform spawn2;

    /// <summary>阵营 B 的席位 3 出生点。</summary>
    [SerializeField] private Transform spawn3;

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
