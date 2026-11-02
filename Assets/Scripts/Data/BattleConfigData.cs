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

    /// <summary>
    /// 技能前摇持续时间，单位为秒。
    /// </summary>
    public float startupSeconds;

    /// <summary>
    /// 技能判定持续时间，单位为秒。
    /// </summary>
    public float activeSeconds;

    /// <summary>
    /// 技能后摇持续时间，单位为秒。
    /// </summary>
    public float recoverySeconds;

    /// <summary>
    /// 技能再次释放前需要等待的时间，单位为秒。
    /// </summary>
    public float cooldownSeconds;

    /// <summary>
    /// 技能造成的伤害值。
    /// </summary>
    public int damage;

    /// <summary>
    /// 技能有效距离，单位为米。
    /// </summary>
    public float range;

    /// <summary>
    /// 技能作用角度，单位为度。
    /// </summary>
    public int angle;

    /// <summary>
    /// 技能产生的位移距离，单位为米。
    /// </summary>
    public float moveDistance;

}
