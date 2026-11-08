using UnityEngine;

/// <summary>
/// 指定地图碰撞层、边界和手动出生点，只供地图制作与烘焙使用。
/// </summary>
public sealed class MapAuthoring : MonoBehaviour
{
    [Min(1)] [SerializeField] private int mapID = 1;
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

    [SerializeField] private CoreBase coreA;
    [SerializeField] private CoreBase coreB;

    [SerializeField] private MonsterSpawner monsterSpawner;

    public int MapID => mapID;
    public LayerMask CollisionLayers => collisionLayers;
    public Bounds MapBounds => new Bounds(transform.position, boundsSize);
    public MonsterSpawner MonsterSpawner => monsterSpawner;

    /// <summary>取得四个手动出生点，烘焙时按席位顺序导出，不自动调整位置。</summary>
    public Vector3[] GetSpawns()
    {
        return new[] { spawn0.position, spawn1.position, spawn2.position, spawn3.position };
    }

    /// <summary>按阵营 A、B 的顺序导出两座基地。</summary>
    public MapCoreData[] GetCores()
    {
        return new[] { coreA.BuildData(), coreB.BuildData() };
    }


    public MapMonsterData GetMonsters() => monsterSpawner == null ? null : monsterSpawner.BuildData();

    /// <summary>
    /// 选中地图范围对象时显示导出区域。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, boundsSize);
    }
}
