using System;
using FixMath.NET;

/// <summary>
/// 客户端和服务端共用的静态地图。三维形状直接使用 Fix64，长度单位为米。
/// </summary>
[Serializable]
public sealed class MapData
{
    /// <summary>
    /// 地图 ID。
    /// </summary>
    public int mapID;

    /// <summary>三维烘焙范围的两个角点；XZ 表示活动边界，Y 只表示收集范围。</summary>
    public MapVector boundsMin;
    public MapVector boundsMax;

    /// <summary>手动摆放的四个脚底出生位置，按席位 0～3 排列。</summary>
    public MapVector[] spawns = Array.Empty<MapVector>();

    /// <summary>双方秘核基地，按阵营 A、阵营 B 的顺序保存。</summary>
    public MapCoreData[] cores = Array.Empty<MapCoreData>();

    /// <summary>中立怪物的刷怪与属性配置；地图不启用怪物时为 null。</summary>
    public MapMonsterData monsters;

    /// <summary>有旋转和尺寸的静态盒体。</summary>
    public MapBox[] boxes = Array.Empty<MapBox>();

    /// <summary>静态球体。</summary>
    public MapSphere[] spheres = Array.Empty<MapSphere>();

    /// <summary>由两端球心与半径描述的静态胶囊体。</summary>
    public MapCapsule[] capsules = Array.Empty<MapCapsule>();

    /// <summary>已应用世界变换的静态三角网格，包括凹凸地面。</summary>
    public MapMesh[] meshes = Array.Empty<MapMesh>();
}

/// <summary>随地图导出的中立怪物配置，各客户端从同一份数据推进刷怪和 AI。</summary>
[Serializable]
public class MapMonsterData
{
    /// <summary>手动标记的刷怪区域中心，按场景数组顺序保存。</summary>
    public MapVector[] spawns = Array.Empty<MapVector>();
    /// <summary>从开战第 0 帧起，首波等待的帧数。</summary>
    public int firstWaveFrame;
    /// <summary>相邻两波之间的逻辑帧数。</summary>
    public int waveIntervalFrames;
    /// <summary>双方合计的每波生成预算，按怪物的生成价格消耗。</summary>
    public int waveBudget = 24;
    /// <summary>每方存活与待生成怪物的强度合计上限。</summary>
    public int maxCampStrength = 48;
    /// <summary>每个逻辑帧最多落地的数量。</summary>
    public int spawnsPerFrame;
    /// <summary>存活数量与等待生成数量的合计上限。</summary>
    public int maxAlive;
    /// <summary>狂暴差达到设定值时，高狂暴方最多获得的预算百分比。</summary>
    public int higherRagePercent = 60;
    /// <summary>达到最大预算倾斜所需的双方狂暴差；差距更小时按比例倾斜。</summary>
    public int rageDifferenceForMaxBias = 100;
    /// <summary>刷怪标记周围可用地面的搜索半径，单位为米。</summary>
    public Fix64 spawnRadius;
    /// <summary>单层 XZ 寻路网格的边长，单位为米。</summary>
    public Fix64 navigationCellSize;
    /// <summary>怪物重新决定移动或攻击的间隔；攻击命中时机仍逐帧检查。</summary>
    public int decisionIntervalFrames;
    public MapMonsterTypeData[] types = Array.Empty<MapMonsterTypeData>();
    /// <summary>圆柱身体的半径与完整高度，单位为米。</summary>
    public Fix64 radius;
    public Fix64 height;
    /// <summary>能够跨越的台阶高度，单位为米。</summary>
    public Fix64 stepHeight;
    /// <summary>可行走的最大坡度，单位为度。</summary>
    public Fix64 maxSlope;
    /// <summary>受到伤害后，持续反击攻击者的逻辑帧数。</summary>
    public int retaliationFrames;
    /// <summary>攻击者离受击地点超过此距离时放弃追击，单位为米。</summary>
    public Fix64 chaseRadius;
    /// <summary>死亡模型保留的帧数；死亡当帧就移除碰撞。</summary>
    public int corpseFrames;
}

/// <summary>一种怪物的战斗数值；碰撞尺寸和寻路能力共用刷怪配置。</summary>
[Serializable]
public class MapMonsterTypeData
{
    public int typeID = 1;
    /// <summary>生成一只消耗的预算，也用于统计该方积压强度。</summary>
    public int spawnCost = 1;
    /// <summary>满足预算和解锁条件时的抽取权重；0 表示不参与生成。</summary>
    public int spawnWeight = 1;
    /// <summary>从第几波开始允许生成，首波为 1。</summary>
    public int unlockWave = 1;
    /// <summary>每方每波最多生成几只；0 表示不单独限制此类型。</summary>
    public int maxPerCampPerWave;
    public int maxHp = 150;
    public int damage = 20;
    public Fix64 moveSpeed;
    /// <summary>怪物中心到目标身体边缘的水平攻击距离。</summary>
    public Fix64 attackRange;
    /// <summary>攻击动画 AtkEvent 的时间，向上取整为逻辑帧。</summary>
    public int attackWindupFrames;
    public int attackDurationFrames;
    public int attackIntervalFrames;
}

/// <summary>一个秘核基地的开局配置，由场景导出，运行时不再读取 Unity 碰撞体。</summary>
[Serializable]
public struct MapCoreData
{
    public BattleCamp camp;
    /// <summary>球形碰撞体的世界中心，单位为米。</summary>
    public MapVector center;
    /// <summary>应用缩放后的球形碰撞半径，也是基地的受击范围。</summary>
    public Fix64 radius;
    /// <summary>归属这座基地的底座盒体，和核心共用生命值。</summary>
    public MapBox[] boxes;
    public int maxHp;
}

/// <summary>
/// 三维定点数值，可直接参与定点计算；JSON 保存各分量内部的 RawValue。
/// </summary>
[Serializable]
public struct MapVector
{
    public Fix64 x;
    public Fix64 y;
    public Fix64 z;
}

/// <summary>世界旋转的四元数，四个分量均为定点数。</summary>
[Serializable]
public struct MapRotation
{
    public Fix64 x;
    public Fix64 y;
    public Fix64 z;
    public Fix64 w;
}

/// <summary>静态盒体，仅保存还原碰撞形状需要的数据。</summary>
[Serializable]
public struct MapBox
{
    /// <summary>世界空间中的盒体中心。</summary>
    public MapVector center;
    /// <summary>应用缩放后的完整边长，不是半长。</summary>
    public MapVector size;
    /// <summary>世界空间中的朝向。</summary>
    public MapRotation rotation;
}

/// <summary>静态球体。</summary>
[Serializable]
public struct MapSphere
{
    /// <summary>世界空间中的球心。</summary>
    public MapVector center;
    /// <summary>应用缩放后的定点半径，单位为米。</summary>
    public Fix64 radius;
}

/// <summary>静态胶囊体。两个球心重合时等价于球体。</summary>
[Serializable]
public struct MapCapsule
{
    /// <summary>世界空间中两端半球的球心，不是胶囊外侧端点。</summary>
    public MapVector pointA;
    public MapVector pointB;
    /// <summary>应用缩放后的定点半径，单位为米。</summary>
    public Fix64 radius;
}

/// <summary>静态三角网格。顶点已包含位移、旋转与缩放，加载时不能重复应用物体变换。</summary>
[Serializable]
public sealed class MapMesh
{
    /// <summary>世界空间中的定点顶点。</summary>
    public MapVector[] vertices = Array.Empty<MapVector>();
    /// <summary>每三个索引构成一个三角形，负缩放产生的绕序变化已在烘焙时处理。</summary>
    public int[] triangles = Array.Empty<int>();
}
