using System;
using System.Collections.Generic;
using BEPUutilities;
using FixMath.NET;

/// <summary>
/// 从定点地图建立单层地面网格。所有怪物共享基地和英雄的寻路距离场，绕墙时不逐只运行 A*。
/// 负责给怪物找最短路径
/// </summary>
public class MonsterNavigation
{
    /// <summary>
    /// 当前定点物理世界
    /// </summary>
    private BattlePhysics physics;
    /// <summary>
    /// 怪物共同配置
    /// </summary>
    private MapMonsterData config;
    private int width;
    private int depth;
    /// <summary>
    /// 每个格子的落脚点
    /// </summary>
    private Vector3[] positions;
    /// <summary>
    /// 对应格子能否站立
    /// </summary>
    private bool[] walkable;
    /// <summary>
    /// 每个格子能够通向哪些邻居
    /// </summary>
    private byte[] links;

    private int[][] distances;
    /// <summary>
    /// 记录每个目标上次所在的格子
    /// </summary>
    private int[] targetCells;
    /// <summary>
    /// 根据怪物类型，找到对应的寻路基地表
    /// </summary>
    private Dictionary<int, int> coreFields = new Dictionary<int, int>();

    public int PlayerField(int seat) => config.types.Length * 2 + seat;

    public int CoreField(int typeID, int campIndex) => coreFields[typeID] + campIndex;
    /// <summary>
    /// BFS使用的队列
    /// </summary>
    private int[] queue;
    // 八个相邻方向的坐标偏移。
    private int[] dx = { 1, 0, -1, 0, 1, -1, -1, 1 };
    private int[] dz = { 0, 1, 0, -1, 1, 1, -1, -1 };

    public MonsterNavigation(BattlePhysics physics, MapMonsterData config)
    {
        this.physics = physics;
        this.config = config;
        distances = new int[config.types.Length * 2 + BattleConst.SeatCount][];
        targetCells = new int[distances.Length];
        for (int type = 0; type < config.types.Length; type++) coreFields.Add(config.types[type].typeID, type * 2);
        width = (int)Fix64.Floor((physics.BoundsMax.X - physics.BoundsMin.X) / config.navigationCellSize);
        depth = (int)Fix64.Floor((physics.BoundsMax.Z - physics.BoundsMin.Z) / config.navigationCellSize);
        int count = width * depth;
        positions = new Vector3[count];
        walkable = new bool[count];
        links = new byte[count];
        queue = new int[count];
        //检查每个格子有没有地面
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                //建立格子编号
                int cell = z * width + x;
                Fix64 worldX = physics.BoundsMin.X + (x + (Fix64)0.5m) * config.navigationCellSize;
                Fix64 worldZ = physics.BoundsMin.Z + (z + (Fix64)0.5m) * config.navigationCellSize;
                walkable[cell] = physics.TryGetMonsterGround(worldX, worldZ, config, out positions[cell]);
            }
        }
        Fix64 slope = Fix64.Sin(config.maxSlope * Fix64.Pi / 180) /
            Fix64.Cos(config.maxSlope * Fix64.Pi / 180);
        for (int cell = 0; cell < count; cell++)
        {
            if (!walkable[cell]) continue;
            for (int direction = 0; direction < dx.Length; direction++)
            {
                int next = Neighbor(cell, direction);
                if (next < 0 || !walkable[next]) continue;
                Vector3 delta = positions[next] - positions[cell];
                Fix64 horizontal = Fix64.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
                if (Fix64.Abs(delta.Y) <= MathHelper.Max(config.stepHeight, horizontal * slope) &&
                    physics.CanMonsterWalk(positions[cell], positions[next], config))
                {
                    links[cell] |= (byte)(1 << direction);
                }
            }
        }
        for (int field = 0; field < distances.Length; field++)
        {
            distances[field] = new int[count];
            targetCells[field] = -1;
            for (int cell = 0; cell < count; cell++) distances[field][cell] = -1;
        }
        for (int type = 0; type < config.types.Length; type++)
        for (int core = 0; core < 2; core++)
        {
            int field = type * 2 + core;
            MapCoreData data = physics.Cores[core];
            Vector3 center = new Vector3(data.center.x, data.center.y, data.center.z);
            int write = 0;
            // 基地周围所有能实际攻击的位置都是终点，不能只选离中心最近的一块高台。
            for (int cell = 0; cell < count; cell++)
            {
                if (walkable[cell] && physics.CanMonsterAttack(positions[cell], center,
                    data.radius, data.radius * 2, config, config.types[type], core))
                {
                    distances[field][cell] = 0;
                    queue[write++] = cell;
                }
            }
            SpreadDistances(field, write);
        }
    }

    /// <summary>目标跨格时重建距离场；同一目标的整群怪物复用计算结果。</summary>
    public void SetTarget(int field, Vector3 target)
    {
        int destination = FindCell(target);
        if (destination == targetCells[field]) return;
        targetCells[field] = destination;
        int[] distance = distances[field];
        for (int cell = 0; cell < distance.Length; cell++) distance[cell] = -1;
        if (destination < 0) return;
        distance[destination] = 0;
        queue[0] = destination;
        SpreadDistances(field, 1);
    }

    /// <summary>从可攻击的终点向外建立距离场，供同目标的怪物共用。</summary>
    private void SpreadDistances(int field, int write)
    {
        int[] distance = distances[field];
        int read = 0;
        while (read < write)
        {
            int current = queue[read++];
            for (int direction = 0; direction < dx.Length; direction++)
            {
                int next = Neighbor(current, direction);
                if (next < 0 || distance[next] >= 0) continue;
                // 从目标反向扩散，检查 next -> current，保留单向可行的台阶连接。
                int reverse = direction < 4 ? (direction + 2) % 4 : 4 + (direction - 4 + 2) % 4;
                if ((links[next] & (1 << reverse)) == 0) continue;
                distance[next] = distance[current] + 1;
                queue[write++] = next;
            }
        }
    }

    public Vector3 GetDirection(Vector3 position, Vector3 targetFeet, int field)
    {
        int cell = FindCell(position);
        if (cell < 0 || distances[field][cell] < 0) return Vector3.Zero;
        int next = cell;
        for (int direction = 0; direction < dx.Length; direction++)
        {
            if ((links[cell] & (1 << direction)) == 0) continue;
            int neighbor = Neighbor(cell, direction);
            if (distances[field][neighbor] >= 0 && distances[field][neighbor] < distances[field][next]) next = neighbor;
        }
        Vector3 destination = next == cell && field >= config.types.Length * 2 ? targetFeet : positions[next];
        // 角色靠近格子拐角时仍检查身体能否通过，不能切角穿墙。
        if (!physics.CanMonsterWalk(position, destination, config)) destination = positions[cell];
        Vector3 move = destination - position;
        move.Y = Fix64.Zero;
        if (move.LengthSquared() > Toolbox.Epsilon) move.Normalize();
        return move;
    }

    /// <summary>收集出生标记附近、至少能通向一座基地的格子；实际刷怪还会排除已被身体占据的位置。</summary>
    public Vector3[] GetSpawnPositions()
    {
        List<Vector3> result = new List<Vector3>();
        Fix64 radiusSquared = config.spawnRadius * config.spawnRadius;
        foreach (MapVector marker in config.spawns)
        {
            for (int cell = 0; cell < positions.Length; cell++)
            {
                if (!walkable[cell] || !ReachesAnyCore(cell)) continue;
                Vector3 delta = positions[cell] - new Vector3(marker.x, marker.y, marker.z);
                if (delta.LengthSquared() <= radiusSquared) result.Add(positions[cell]);
            }
        }
        if (result.Count == 0)
        {
            throw new InvalidOperationException("怪物出生区域没有可通行且连到基地的地面，请调整出生标记、半径或寻路格子尺寸。");
        }
        return result.ToArray();
    }

    private bool ReachesAnyCore(int cell)
    {
        for (int field = 0; field < config.types.Length * 2; field++)
            if (distances[field][cell] >= 0) return true;
        return false;
    }

    public bool CanReach(Vector3 position, int field)
    {
        int cell = FindCell(position);
        return cell >= 0 && distances[field][cell] >= 0;
    }

    private int FindCell(Vector3 position)
    {
        int best = -1;
        Fix64 bestDistance = Fix64.MaxValue;
        int gridX = (int)Fix64.Floor((position.X - physics.BoundsMin.X) / config.navigationCellSize);
        int gridZ = (int)Fix64.Floor((position.Z - physics.BoundsMin.Z) / config.navigationCellSize);
        for (int z = Math.Max(0, gridZ - 2); z <= Math.Min(depth - 1, gridZ + 2); z++)
        {
            for (int x = Math.Max(0, gridX - 2); x <= Math.Min(width - 1, gridX + 2); x++)
            {
                int cell = z * width + x;
                if (!walkable[cell]) continue;
                Fix64 distance = (positions[cell] - position).LengthSquared();
                if (distance < bestDistance) { bestDistance = distance; best = cell; }
            }
        }
        if (best >= 0) return best;
        // 基地中心可能位于大片建筑内部；目标映射到最近的可行格，不能把建筑内部当作道路。
        for (int cell = 0; cell < positions.Length; cell++)
        {
            if (!walkable[cell]) continue;
            Fix64 distance = (positions[cell] - position).LengthSquared();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = cell;
            }
        }
        return best;
    }

    private int Neighbor(int cell, int direction)
    {
        int x = cell % width + dx[direction];
        int z = cell / width + dz[direction];
        return x < 0 || x >= width || z < 0 || z >= depth ? -1 : z * width + x;
    }
}
