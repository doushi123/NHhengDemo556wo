using System;

/// <summary>
/// 当前战斗配置文件中的全部确定性数值。
/// 客户端和服务端都从同一份 JSON 恢复该结构。
/// </summary>
[Serializable]
public sealed class BattleConfigData
{
    /// <summary>
    /// 全部英雄配置。
    /// </summary>
    public HeroConfigData[] heroes = Array.Empty<HeroConfigData>();

    /// <summary>
    /// 全部技能配置。
    /// </summary>
    public SkillConfigData[] skills = Array.Empty<SkillConfigData>();
}

/// <summary>
/// 一个英雄参与确定性模拟所需的固定配置。
/// </summary>
[Serializable]
public sealed class HeroConfigData
{
    /// <summary>
    /// 唯一 ID。
    /// </summary>
    public int heroID;

    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int maxHp;

    /// <summary>
    /// 走路速度，单位为米/秒。
    /// </summary>
    public float moveSpeed;

    /// <summary>
    /// 跑步速度，单位为米/秒。
    /// </summary>
    public float runSpeed;

    /// <summary>
    /// 碰撞半径，单位为米。
    /// </summary>
    public float collisionRadius;

    /// <summary>站立碰撞体的完整高度，单位为米；模型根节点按脚底对齐。</summary>
    public float collisionHeight = 1.8f;

    /// <summary>能够直接跨上的台阶最大高度，单位为米。</summary>
    public float stepHeight = 0.35f;

    /// <summary>可正常行走的最大坡度，单位为度；更陡的表面按滑动处理。</summary>
    public float maxSlope = 45f;

    /// <summary>
    /// 技能一使用的技能 ID；0 表示尚未配置。
    /// </summary>
    public int skill1ID;

    /// <summary>
    /// 技能二使用的技能 ID；0 表示尚未配置。
    /// </summary>
    public int skill2ID;

    /// <summary>电脑操控该英雄时使用的行为参数。</summary>
    public BotConfigData bot = new BotConfigData();
}

/// <summary>确定性电脑玩家的决策参数。</summary>
[Serializable]
public class BotConfigData
{
    /// <summary>两次重新选择目标和动作之间的逻辑帧数。</summary>
    public int decisionIntervalFrames = 3;
    /// <summary>距我方基地碰撞边缘多少米内的威胁需要优先处理。</summary>
    public float coreDefenseRange = 12f;
    /// <summary>没有基地威胁时，主动攻击自身多少米内的中立怪物。</summary>
    public float monsterNoticeRange = 8f;
}

/// <summary>
/// 技能在玩法中的用途分类。
/// 这里描述技能做什么，不描述它采用近战、远程等表现形式。
/// </summary>
public enum SkillType
{
    /// <summary>
    /// 对目标造成伤害。
    /// </summary>
    Attack = 1,

    /// <summary>
    /// 用于防御、闪避或减免伤害。
    /// </summary>
    Defense = 2,
}

/// <summary>技能如何产生命中，与攻击、防御等用途分类分开。</summary>
public enum SkillEffect
{
    /// <summary>按角色前方的水平扇形检查命中。</summary>
    Sector = 0,
    /// <summary>前摇结束时发射剑气，由定点物理检查飞行中的碰撞。</summary>
    SwordWave = 1,
}

/// <summary>
/// 一个技能参与确定性模拟所需的固定配置。
/// 时间配置使用秒保存，进入战斗模拟时再按固定逻辑帧率转换。
/// </summary>
[Serializable]
public sealed class SkillConfigData
{
    /// <summary>
    /// 技能配置的唯一 ID。
    /// </summary>
    public int skillID;

    /// <summary>
    /// 技能类型。
    /// </summary>
    public SkillType skillType;

    /// <summary>技能使用的命中方式；普通攻击使用 Sector。</summary>
    public SkillEffect effect;

    /// <summary>
    /// 技能前摇持续时间，单位为秒；绑定动画时由开头到 HitStart 导出。
    /// </summary>
    public float startupSeconds;

    /// <summary>
    /// 技能动作的判定段时间，单位为秒，由 HitStart 到 HitEnd 导出。
    /// 剑气在这一段的起点发射，离手后独立飞行，不随收招消失。
    /// 冲刺配置 2002 使用该字段表示位移持续时间，不读取攻击事件。
    /// </summary>
    public float activeSeconds;

    /// <summary>
    /// 技能后摇持续时间，单位为秒；绑定动画时由 HitEnd 到片段结尾导出。
    /// </summary>
    public float recoverySeconds;

    /// <summary>
    /// 从起手到允许再次释放需要等待的时间，单位为秒；仍需等本次动作结束。
    /// </summary>
    public float cooldownSeconds;

    /// <summary>
    /// 技能造成的伤害值。
    /// </summary>
    public int damage;

    /// <summary>
    /// 扇形从角色中心向外的半径，或剑气最大飞行距离，单位为米。
    /// </summary>
    public float range;

    /// <summary>
    /// 扇形的完整夹角，单位为度，范围为 1～360；以角色前方为中心向两侧展开。
    /// </summary>
    public int angle;

    /// <summary>
    /// 技能产生的位移距离，单位为米；冲刺 2002 表示无阻挡时的水平移动距离。
    /// </summary>
    public float moveDistance;

    /// <summary>剑气每秒飞行的米数，只用于 SwordWave。</summary>
    public float projectileSpeed = 12f;

    /// <summary>剑气球形判定体的半径，单位为米，只用于 SwordWave。</summary>
    public float projectileRadius = 0.5f;

}
