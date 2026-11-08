using BEPUutilities;
using FixMath.NET;

/// <summary>一道已发射的剑气，由确定性模拟推进，粒子只读取它的位置。</summary>
public struct SwordWaveState
{
    /// <summary>本局内递增的编号，供表现区分不同剑气。</summary>
    public int id;
    /// <summary>发射者的席位，命中时据此排除友军。</summary>
    public int ownerSeat;
    /// <summary>发射时的逻辑帧，供粒子在追帧后显示正确进度。</summary>
    public int spawnFrame;
    /// <summary>是否已经命中或飞到终点；保留到下一帧，供表现读取最后位置。</summary>
    public bool finished;
    /// <summary>上一逻辑帧末的位置，供画面插值。</summary>
    public Vector3 previousPosition;
    /// <summary>当前球形判定体的中心，单位为米。</summary>
    public Vector3 position;
    /// <summary>发射时确定的单位方向，离手后不随角色转向。</summary>
    public Vector3 direction;
    /// <summary>还能飞行的距离，单位为米，耗尽后移除。</summary>
    public Fix64 remainingDistance;
}
