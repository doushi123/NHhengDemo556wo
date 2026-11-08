using BEPUutilities;

/// <summary>一个席位的冲刺方向和结束帧，由操作帧推进。</summary>
public struct DodgeState
{
    /// <summary>本次冲刺开始的逻辑帧。</summary>
    public int startFrame;
    /// <summary>到这一帧恢复普通移动和攻击。</summary>
    public int endFrame;
    /// <summary>起步时确定的单位方向，冲刺中不转向。</summary>
    public Vector3 direction;
}
