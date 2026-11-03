using System;
using BEPUphysics;
using BEPUphysics.BroadPhaseEntries;
using BEPUphysics.Character;
using BEPUphysics.Entities.Prefabs;
using BEPUutilities;
using FixMath.NET;

/// <summary>建立地图碰撞和角色身体</summary>
public sealed class BattlePhysics
{
    private Vector3 min;
    private Vector3 max;

    public int mapID { get; }
    /// <summary>
    /// BEPU的物理世界
    /// </summary>
    public Space Space { get; }

    /// <summary>将地图数据变成BEPU地面、障碍物，设置重力和计算步长</summary>
    public BattlePhysics(MapData map)
    {
        mapID = map.mapID;
        min = ToVector(map.boundsMin);
        max = ToVector(map.boundsMax);
        if (max.X <= min.X || max.Y <= min.Y || max.Z <= min.Z)
        {
            throw new InvalidOperationException("三维地图范围无效，请重新导出地图。");
        }

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
    }

    

    /// <summary>创建 BEPU 的圆柱角色控制器，负责重力、支撑、爬坡、跨台阶及角色间碰撞。</summary>
    public CharacterController CreatePlayer(Vector3 feet, HeroConfigData hero)
    {
        Fix64 radius = (Fix64)hero.collisionRadius;
        Fix64 height = (Fix64)hero.collisionHeight;
        if (!Contains(feet, radius))
        {
            throw new InvalidOperationException("角色出生点超出地图 XZ 边界：" + feet);
        }

        CharacterController player = new CharacterController(
            feet + new Vector3(0, height / 2, 0), height: height,
            radius: radius, margin: MathHelper.Min(radius, height) / 10,
            standingSpeed: (Fix64)hero.moveSpeed,
            maximumTractionSlope: (Fix64)hero.maxSlope * Fix64.Pi / 180,
            maximumSupportSlope: (Fix64)PhysicsConst.SupportSlope * Fix64.Pi / 180);
        player.IsUpdatedSequentially = true;
        player.StepManager.MaximumStepHeight = (Fix64)hero.stepHeight;
        Space.Add(player);
        return player;
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
