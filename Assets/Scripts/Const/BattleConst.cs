/// <summary>
/// 客户端与服务端共用的战斗常量。
/// </summary>
public static class BattleConst
{
    /// <summary>
    /// 确定性战斗每秒推进的逻辑帧数量。
    /// </summary>
    public const int LogicFrameRate = 20;
    /// <summary>
    /// 所有英雄共用的普通攻击技能 ID。
    /// </summary>
    public const int BasicAttackSkillID = 2001;

    /// <summary>
    /// 所有英雄共用的闪避技能 ID。
    /// </summary>
    public const int DodgeSkillID = 2002;

    /// <summary>
    /// 客户端与服务端共同读取的战斗配置文件名。
    /// </summary>
    public const string ConfigFileName = "BattleConfig.json";

    /// <summary>
    /// 一场战斗固定包含的英雄席位数量。
    /// </summary>
    public const int SeatCount = 4;

    /// <summary>
    /// 服务端生成战斗 Token 时使用的随机字节数。
    /// </summary>
    public const int TokenRandomBytes = 16;
}
