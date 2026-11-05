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

    /// <summary>有旋转和尺寸的静态盒体。</summary>
    public MapBox[] boxes = Array.Empty<MapBox>();

    /// <summary>静态球体。</summary>
    public MapSphere[] spheres = Array.Empty<MapSphere>();

    /// <summary>由两端球心与半径描述的静态胶囊体。</summary>
    public MapCapsule[] capsules = Array.Empty<MapCapsule>();

    /// <summary>已应用世界变换的静态三角网格，包括凹凸地面。</summary>
    public MapMesh[] meshes = Array.Empty<MapMesh>();
}

/// <summary>
/// 三维定点数值，可直接参与定点计算；JSON 保存各分量内部的 RawValue，不经过浮点数转换。
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
