/// <summary>普攻或主动技能当前所处的动作阶段。</summary>
public enum AttackPhase
{
    /// <summary>没有正在执行的攻击动作。</summary>
    None,
    /// <summary>前摇：从动作起手到 HitStart。</summary>
    Startup,
    /// <summary>攻击有效阶段：HitStart 到 HitEnd。</summary>
    Active,
    /// <summary>后摇（收招）：HitEnd 到动作结束。</summary>
    Recovery,
}

/// <summary>一个席位正在执行的招式，由操作帧推进，动画只读取。</summary>
public struct AttackState
{
    /// <summary>当前动作使用的技能 ID，普通攻击与主动技能共用动作时序。</summary>
    public int skillID;
    /// <summary>当前动作阶段：无动作、前摇、攻击有效阶段或后摇。</summary>
    public AttackPhase phase;
    /// <summary>本次攻击起手的逻辑帧，用于识别一次新的攻击。</summary>
    public int startFrame;
    /// <summary>从起手到当前已经经过的逻辑帧数。</summary>
    public int elapsedFrames;
    /// <summary>本次已命中的目标：第 0～3 位是角色，第 4、5 位是双方基地，防止一刀重复扣血。</summary>
    public int hitMask;
}
