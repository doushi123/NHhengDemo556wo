using FixMath.NET;

/// <summary>将技能配置中的秒数转换成逻辑帧，供动作阶段和动画进度使用。</summary>
public struct AttackTiming
{
    /// <summary>有效判定期开始时，相对本次起手的逻辑帧。</summary>
    public int hitStartFrame;
    /// <summary>有效判定期结束、进入后摇的逻辑帧。</summary>
    public int hitEndFrame;
    /// <summary>本次动作结束所需的逻辑帧数。</summary>
    public int durationFrames;
    /// <summary>从起手开始计算的冷却帧数。</summary>
    public int cooldownFrames;
    /// <summary>
    /// 保存动画原始的前摇秒数
    /// </summary>
    private Fix64 startup;
    /// <summary>
    /// 保存动画原始的攻击有效阶段秒数
    /// </summary>
    private Fix64 active;
    /// <summary>
    /// 保存动画原始的后摇秒数
    /// </summary>
    private Fix64 recovery;

    public AttackTiming(SkillConfigData skill)
    {
        // 累计秒数后再取整，避免分别取整把动作拉长。
        decimal start = (decimal)skill.startupSeconds;
        decimal end = start + (decimal)skill.activeSeconds;
        hitStartFrame = ToFrames(start);
        hitEndFrame = ToFrames(end);
        durationFrames = ToFrames(end + (decimal)skill.recoverySeconds);
        cooldownFrames = ToFrames((decimal)skill.cooldownSeconds);
        startup = (Fix64)start;
        active = (Fix64)(decimal)skill.activeSeconds;
        recovery = (Fix64)(decimal)skill.recoverySeconds;
    }

    /// <summary>按阶段映射回动画时间，使逻辑判定与画面的 HitStart、HitEnd 对齐。</summary>
    public Fix64 GetNormalizedTime(Fix64 elapsedFrames)
    {
        Fix64 seconds;
        if (elapsedFrames < hitStartFrame)
        {
            seconds = startup * elapsedFrames / hitStartFrame;
        }
        else if (elapsedFrames < hitEndFrame)
        {
            seconds = startup + active * (elapsedFrames - hitStartFrame) / (hitEndFrame - hitStartFrame);
        }
        else if (elapsedFrames < durationFrames)
        {
            seconds = startup + active + recovery * (elapsedFrames - hitEndFrame) / (durationFrames - hitEndFrame);
        }
        else
        {
            return Fix64.One;
        }
        return seconds / (startup + active + recovery);
    }

    private static int ToFrames(decimal seconds)
    {
        return (int)Fix64.Ceiling((Fix64)seconds * BattleConst.LogicFrameRate);
    }
}
