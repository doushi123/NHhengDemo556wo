using System;
using BEPUphysics;
using BEPUphysics.BroadPhaseEntries;
using BEPUphysics.Character;
using BEPUphysics.BroadPhaseEntries.MobileCollidables;
using BEPUphysics.CollisionShapes.ConvexShapes;
using BEPUphysics.CollisionTests.CollisionAlgorithms.GJK;
using BEPUphysics.Entities.Prefabs;
using BEPUutilities;
using FixMath.NET;

/// <summary>建立地图碰撞和角色身体</summary>
public sealed class BattlePhysics
{
    private Vector3 min;
    private Vector3 max;
    // 剑气查询复用球形状，不向物理世界添加会推挤角色的攻击实体。
    private SphereShape swordWaveShape = new SphereShape((Fix64)0.5m);
    private Func<BroadPhaseEntry, bool> mapFilter;
    private Func<BroadPhaseEntry, bool> obstacleFilter;
    private BEPUphysics.Entities.Entity[][] coreBodies = new BEPUphysics.Entities.Entity[2][];
    private Box[][] coreBases = new Box[2][];

    public Box[] GetCoreBases(int core) => coreBases[core];
    private int ignoredCore = -1;
    private CylinderShape navigationShape;

    public MapCoreData[] Cores { get; }
    public MapMonsterData Monsters { get; }
    public Vector3 BoundsMin => min;
    public Vector3 BoundsMax => max;

    public int mapID { get; }
    /// <summary>
    /// BEPU的物理世界
    /// </summary>
    public Space Space { get; }

    /// <summary>将地图数据变成BEPU地面、障碍物，设置重力和计算步长</summary>
    public BattlePhysics(MapData map)
    {
        mapID = map.mapID;
        Cores = map.cores;
        Monsters = map.monsters;
        mapFilter = IsMapGeometry;
        obstacleFilter = IsObstacle;
        min = ToVector(map.boundsMin);
        max = ToVector(map.boundsMax);

        // 不传线程池，避免碰撞和约束求解顺序随线程调度改变。
        Space = new Space();
        Space.ForceUpdater.Gravity = new Vector3(0, (Fix64)PhysicsConst.Gravity, 0);
        Space.TimeStepSettings.TimeStepDuration =
            Fix64.One / (BattleConst.LogicFrameRate * PhysicsConst.PhysicsSubsteps);

        foreach (MapBox data in map.boxes)
        {
            Vector3 size = ToVector(data.size);
            Quaternion rotation = new Quaternion(data.rotation.x,data.rotation.y, data.rotation.z, data.rotation.w);
            rotation.Normalize();
            Box box = new Box(ToVector(data.center), size.X, size.Y, size.Z);
            box.Orientation = rotation;
            Space.Add(box);
        }
        foreach (MapSphere data in map.spheres)
        {
            Space.Add(new Sphere(ToVector(data.center), data.radius));
        }
        foreach (MapCapsule data in map.capsules)
        {
            Space.Add(new Capsule(ToVector(data.pointA), ToVector(data.pointB), data.radius));
        }
        foreach (MapMesh data in map.meshes)
        {
            Vector3[] vertices = new Vector3[data.vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = ToVector(data.vertices[i]);
            }
            // 顶点已经是世界坐标，不能再次应用 Unity Transform。
            Space.Add(new StaticMesh(vertices, data.triangles));
        }
        foreach (MapCoreData data in Cores)
        {
            int index = (int)data.camp - (int)BattleCamp.CampA;
            Sphere body = new Sphere(ToVector(data.center), data.radius);
            MapBox[] boxes = data.boxes ?? Array.Empty<MapBox>();
            coreBodies[index] = new BEPUphysics.Entities.Entity[1 + boxes.Length];
            coreBases[index] = new Box[boxes.Length];
            coreBodies[index][0] = body;
            Space.Add(body);
            for (int part = 0; part < boxes.Length; part++)
            {
                MapBox box = boxes[part];
                Box basis = new Box(ToVector(box.center), box.size.x, box.size.y, box.size.z);
                Quaternion rotation = new Quaternion(box.rotation.x, box.rotation.y, box.rotation.z, box.rotation.w);
                rotation.Normalize();
                basis.Orientation = rotation;
                coreBases[index][part] = basis;
                coreBodies[index][part + 1] = basis;
                Space.Add(basis);
            }
        }
    }

    /// <summary>基地摧毁后移除身体，角色和剑气可以通过原位置。</summary>
    public void RemoveCore(int index)
    {
        foreach (BEPUphysics.Entities.Entity part in coreBodies[index]) Space.Remove(part);
    }

    /// <summary>扫掠剑气与核心、底座，取整座基地的首次命中；起始重叠也算命中。</summary>
    public bool CastSwordWaveOnCore(Vector3 from, Vector3 sweep, Fix64 radius, int index, out Fix64 time)
    {
        swordWaveShape.Radius = radius;
        RigidTransform start = new RigidTransform(from);
        time = Fix64.One;
        bool collided = false;
        foreach (BEPUphysics.Entities.Entity part in coreBodies[index])
        {
            RigidTransform body = part.CollisionInformation.WorldTransform;
            ConvexShape shape = (ConvexShape)part.CollisionInformation.Shape;
            if (GJKToolbox.AreShapesIntersecting(swordWaveShape, shape, ref start, ref body))
            {
                time = Fix64.Zero;
                return true;
            }
            Vector3 targetSweep = Vector3.Zero;
            if (GJKToolbox.ConvexCast(swordWaveShape, shape, ref sweep, ref targetSweep,
                ref start, ref body, out RayHit hit) && (!collided || hit.T < time))
            {
                time = MathHelper.Clamp(hit.T, Fix64.Zero, Fix64.One);
                collided = true;
            }
        }
        return collided;
    }

    /// <summary>创建 BEPU 的圆柱角色控制器，负责重力、支撑、爬坡、跨台阶及角色间碰撞。</summary>
    public CharacterController CreatePlayer(Vector3 feet, HeroConfigData hero)
    {
        return CreateCharacter(feet, (Fix64)hero.collisionRadius, (Fix64)hero.collisionHeight,
            (Fix64)hero.moveSpeed, (Fix64)hero.stepHeight, (Fix64)hero.maxSlope);
    }

    public CharacterController CreateMonster(Vector3 feet, MapMonsterData data, MapMonsterTypeData type)
    {
        return CreateCharacter(feet, data.radius, data.height, type.moveSpeed, data.stepHeight, data.maxSlope);
    }

    /// <summary>英雄和怪物使用相同的定点角色控制器；尺寸、速度和爬坡能力各取自己的配置。</summary>
    private CharacterController CreateCharacter(Vector3 feet, Fix64 radius, Fix64 height, Fix64 speed,
        Fix64 stepHeight, Fix64 maxSlope)
    {
        if (!Contains(feet, radius))
        {
            throw new InvalidOperationException("角色出生点超出地图 XZ 边界：" + feet);
        }

        CharacterController player = new CharacterController(
            feet + new Vector3(0, height / 2, 0), height: height,
            radius: radius, margin: MathHelper.Min(radius, height) / 10,
            standingSpeed: speed,
            maximumTractionSlope: maxSlope * Fix64.Pi / 180,
            maximumSupportSlope: (Fix64)PhysicsConst.SupportSlope * Fix64.Pi / 180);
        player.IsUpdatedSequentially = true;
        player.StepManager.MaximumStepHeight = stepHeight;
        Space.Add(player);
        return player;
    }

    /// <summary>寻路网格从定点地图向下采样，只接受怪物能站立的坡面。</summary>
    public bool TryGetMonsterGround(Fix64 x, Fix64 z, MapMonsterData data, out Vector3 feet)
    {
        Ray ray = new Ray(new Vector3(x, max.Y, z), new Vector3(0, -1, 0));
        feet = default;
        if (!Space.RayCast(ray, max.Y - min.Y, mapFilter, out RayCastResult hit))
        {
            return false;
        }
        Vector3 normal = hit.HitData.Normal;
        normal.Normalize();
        feet = hit.HitData.Location + new Vector3(0, (Fix64)0.01m, 0);
        return Fix64.Abs(normal.Y) >= Fix64.Cos(data.maxSlope * Fix64.Pi / 180) &&
            Contains(feet, data.radius) && CanMonsterWalk(feet, feet, data);
    }

    /// <summary>用怪物身体扫掠路径，包含基地；脚下允许跨越的高度交给角色台阶机制。</summary>
    public bool CanMonsterWalk(Vector3 from, Vector3 to, MapMonsterData data)
    {
        if (navigationShape == null)
        {
            navigationShape = new CylinderShape(data.height - data.stepHeight, data.radius);
            navigationShape.CollisionMargin = (Fix64)0.01m;
        }
        RigidTransform start = new RigidTransform(from + new Vector3(0,
            (data.height + data.stepHeight) / 2 + (Fix64)0.02m, 0));
        Vector3 sweep = to - from;
        // 站立点也做极短扫掠，排除墙体和基地内部。
        if (sweep.LengthSquared() == Fix64.Zero)
        {
            sweep.Y = (Fix64)0.001m;
        }
        ignoredCore = -1;
        return !Space.ConvexCast(navigationShape, ref start, ref sweep, obstacleFilter, out RayCastResult hit);
    }

    /// <summary>检查怪物攻击距离、高度和遮挡，寻路终点与实际命中使用同一套规则。</summary>
    public bool CanMonsterAttack(Vector3 feet, Vector3 target, Fix64 radius, Fix64 height,
        MapMonsterData data, MapMonsterTypeData type, int targetCore = -1)
    {
        Vector3 center = feet + new Vector3(0, data.height / 2, 0);
        Vector3 offset = target - center;
        Fix64 range = type.attackRange + radius;
        if (Fix64.Abs(offset.Y) <= (height + data.height) / 2 &&
            offset.X * offset.X + offset.Z * offset.Z <= range * range &&
            !IsBlocked(center, target, targetCore)) return true;
        if (targetCore < 0) return false;
        foreach (Box basis in coreBases[targetCore])
        {
            Vector3 contact = ClosestPoint(basis, center);
            offset = contact - center;
            if (Fix64.Abs(offset.Y) <= data.height / 2 &&
                offset.X * offset.X + offset.Z * offset.Z <= type.attackRange * type.attackRange &&
                !IsBlocked(center, contact, targetCore)) return true;
        }
        return false;
    }

    /// <summary>只限制 XZ 活动边界；Y 继续由真实地形和重力计算。</summary>
    public void KeepInBounds(CharacterController player)
    {
        Vector3 position = player.Body.Position;
        Fix64 x = MathHelper.Clamp(position.X, min.X + player.Body.Radius, max.X - player.Body.Radius);
        Fix64 z = MathHelper.Clamp(position.Z, min.Z + player.Body.Radius, max.Z - player.Body.Radius);
        if (x == position.X && z == position.Z)
        {
            return;
        }

        Vector3 velocity = player.Body.LinearVelocity;
        if (x != position.X)
        {
            velocity.X = 0;
        }
        if (z != position.Z)
        {
            velocity.Z = 0;
        }
        player.Body.Position = new Vector3(x, position.Y, z);
        player.Body.LinearVelocity = velocity;
        player.HorizontalMotionConstraint.ResetPositionAnchor();
    }

    /// <summary>用完整球形剑气扫掠静态地图，返回首次碰撞在本段位移中的比例。</summary>
    public bool CastSwordWaveOnMap(Vector3 from, Vector3 sweep, Fix64 radius, out Fix64 time)
    {
        swordWaveShape.Radius = radius;
        RigidTransform start = new RigidTransform(from);
        if (Space.ConvexCast(swordWaveShape, ref start, ref sweep, mapFilter, out RayCastResult hit))
        {
            time = MathHelper.Clamp(hit.HitData.T, Fix64.Zero, Fix64.One);
            return true;
        }
        time = Fix64.One;
        return false;
    }

    /// <summary>扫掠剑气与移动中的角色身体，起始重叠也算命中。</summary>
    public bool CastSwordWaveOnPlayer(Vector3 from, Vector3 sweep, Fix64 radius, CharacterController target,
        Vector3 targetFrom, Vector3 targetTo, out Fix64 time)
    {
        swordWaveShape.Radius = radius;
        RigidTransform start = new RigidTransform(from);
        RigidTransform body = target.Body.CollisionInformation.WorldTransform;
        body.Position = targetFrom;
        if (GJKToolbox.AreShapesIntersecting(swordWaveShape, target.Body.CollisionInformation.Shape, ref start, ref body))
        {
            time = Fix64.Zero;
            return true;
        }
        Vector3 targetSweep = targetTo - targetFrom;
        if (GJKToolbox.ConvexCast(swordWaveShape, target.Body.CollisionInformation.Shape,
            ref sweep, ref targetSweep, ref start, ref body, out RayHit hit))
        {
            time = MathHelper.Clamp(hit.T, Fix64.Zero, Fix64.One);
            return true;
        }
        time = Fix64.One;
        return false;
    }

    /// <summary>检查两点之间是否有静态地图阻挡，用于拦住隔墙攻击。</summary>
    public bool IsBlocked(Vector3 from, Vector3 to, int targetCore = -1)
    {
        Vector3 direction = to - from;
        if (direction.LengthSquared() <= Toolbox.Epsilon)
        {
            return false;
        }
        Fix64 end = Fix64.One;
        if (targetCore >= 0)
        {
            // 底座与核心是一座基地；到达任一部分的表面即可，不再检查它后面的地形。
            Ray ray = new Ray(from, direction);
            foreach (BEPUphysics.Entities.Entity part in coreBodies[targetCore])
            {
                if (part.CollisionInformation.RayCast(ray, end, out RayHit coreHit))
                    end = MathHelper.Clamp(coreHit.T, Fix64.Zero, end);
            }
        }
        ignoredCore = targetCore;
        return Space.RayCast(new Ray(from, direction), end, obstacleFilter, out RayCastResult hit) &&
            hit.HitData.T < end;
    }

    private int CoreIndex(BroadPhaseEntry entry)
    {
        for (int core = 0; core < coreBodies.Length; core++)
            foreach (BEPUphysics.Entities.Entity part in coreBodies[core])
                if (entry == part.CollisionInformation) return core;
        return -1;
    }

    private bool IsMapGeometry(BroadPhaseEntry entry)
    {
        return CoreIndex(entry) < 0 &&
            (entry is StaticMesh || (entry is EntityCollidable body && !body.Entity.IsDynamic));
    }

    // 只排除正在攻击的整座基地，其他基地和普通地形仍会阻挡攻击。
    private bool IsObstacle(BroadPhaseEntry entry)
    {
        return (ignoredCore < 0 || CoreIndex(entry) != ignoredCore) &&
            (entry is StaticMesh || (entry is EntityCollidable body && !body.Entity.IsDynamic));
    }

    public static Vector3 ClosestPoint(Box box, Vector3 position)
    {
        Vector3 local = Quaternion.Transform(position - box.Position, Quaternion.Conjugate(box.Orientation));
        local.X = MathHelper.Clamp(local.X, -box.Width / 2, box.Width / 2);
        local.Y = MathHelper.Clamp(local.Y, -box.Height / 2, box.Height / 2);
        local.Z = MathHelper.Clamp(local.Z, -box.Length / 2, box.Length / 2);
        return box.Position + Quaternion.Transform(local, box.Orientation);
    }

    /// <summary>角色整个水平占地必须在地图活动范围内。</summary>
    private bool Contains(Vector3 feet, Fix64 radius)
    {
        return feet.X - radius >= min.X && feet.X + radius <= max.X &&
            feet.Z - radius >= min.Z && feet.Z + radius <= max.Z;
    }

    /// <summary>把地图定点向量转为物理库向量。</summary>
    private static Vector3 ToVector(MapVector value)
    {
        return new Vector3(value.x, value.y, value.z);
    }
}
