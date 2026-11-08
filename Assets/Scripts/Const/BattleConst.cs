/// <summary>
/// 客户端与服务端共用的战斗常量。
/// </summary>
public static class BattleConst
{
    /// <summary>
    /// 确定性战斗每秒推进的逻辑帧数量。
    /// </summary>
    public const int LogicFrameRate = 20;

    /// <summary>输入提前发送到未来的逻辑帧，给 KCP 传输留出时间。</summary>
    public const int InputDelayFrames = 2;
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

    /// <summary>等待所有真人完成入场的最长秒数，超时取消本次开战。</summary>
    public const int LoadTimeoutSeconds = 120;

    /// <summary>英雄死亡后，等待多久在本席位的手动出生点复活。</summary>
    public const float RespawnSeconds = 5f;

    /// <summary>每个阵营开局时的秘核狂暴。</summary>
    public const int StartingCoreRage = 100;
    /// <summary>本阵营击杀一名敌方英雄时恢复的基地生命（秘核能量）。</summary>
    public const int KillCoreEnergy = 10;
    /// <summary>本阵营英雄死亡时扣除的基地生命（秘核能量）。</summary>
    public const int DeathCoreEnergy = 5;
    /// <summary>本阵营击杀一名敌方英雄时增加的秘核狂暴。</summary>
    public const int KillCoreRage = 20;
    /// <summary>本阵营英雄死亡时减少的秘核狂暴。</summary>
    public const int DeathCoreRage = 10;
    /// <summary>每经过一秒自然减少的秘核狂暴。</summary>
    public const int CoreRageDecayPerSecond = 1;

    /// <summary>基地每累计损失最大生命的 1%，所属阵营增加的秘核狂暴。</summary>
    public const int CoreDamageRageGainPerPercent = 1;

}
