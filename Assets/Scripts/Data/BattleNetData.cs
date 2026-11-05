using System;

/// <summary>
/// 当前支持的战斗模式。
/// 后续新增模式时继续在这里分配固定值。
/// </summary>
public enum BattleMode
{
    /// <summary>
    /// 两支英雄队伍与怪物阵营共同参与的核心 2v2vE 模式。
    /// </summary>
    CoreWar = 1,
}

/// <summary>
/// 战斗中的阵营。
/// CampA 和 CampB 各有两个英雄席位，Monster 不占英雄席位。
/// </summary>
public enum BattleCamp
{
    CampA = 1,
    CampB = 2,
    Monster = 3,
}

/// <summary>
/// 服务端校验战斗 Token 后得到的结果。
/// 客户端只区分认证成功或失败，具体失败原因由服务端记录。
/// </summary>
public enum BattleAuthResult
{
    Success = 0,

    /// <summary>
    /// 无法通过战斗认证。
    /// 客户端统一提示战斗不存在或已经结束。
    /// </summary>
    Failed = 1,
}

/// <summary>
/// KCP 战斗消息的类型编号。
/// 每个战斗包的开头都是该枚举，接收端据此选择对应的解码方法。
/// </summary>
public enum BattlePacketType
{
    /// <summary>
    /// 客户端发给服务端的战斗认证消息
    /// </summary>
    Auth = 1,
    /// <summary>
    /// 服务端返回给客户端的战斗认证结果
    /// 校验battleToken，并返回认证结果
    /// </summary>
    AuthResult = 2,
    /// <summary>
    /// 服务端向客户端下发战斗的初始化数据
    /// </summary>
    Init = 3,
    /// <summary>
    /// 客户端向服务端上传的一帧本地操作
    /// </summary>
    PlayerInput = 4,

    /// <summary>服务端确认并广播给所有客户端的一帧操作。</summary>
    AuthorityFrame = 5,

    /// <summary>客户端确认已加载好。</summary>
    Ready = 6,

    /// <summary>所有客户端已加载完毕，服务端通知各客户端开始战斗。</summary>
    Start = 7,
}

/// <summary>
/// 标记一个结构体是 KCP 战斗消息，并指定它的消息类型。
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class BattlePacketAttribute : Attribute
{
    public BattlePacketType Type { get; }

    public BattlePacketAttribute(BattlePacketType type)
    {
        Type = type;
    }
}

/// <summary>
/// KCP 连接建立后，客户端发送的第一条业务消息。
/// </summary>
[BattlePacket(BattlePacketType.Auth)]
public struct BattleAuthPacket
{
    /// <summary>
    /// 服务端通过已登录的 TCP 连接签发的临时战斗凭证token。
    /// </summary>
    [NetField(1)]
    public string battleToken;
}

/// <summary>
/// 服务端返回的 KCP 战斗认证结果。
/// </summary>
[BattlePacket(BattlePacketType.AuthResult)]
public struct BattleAuthResultPacket
{
    /// <summary>
    /// 本次认证的明确结果。
    /// </summary>
    [NetField(1)]
    public BattleAuthResult result;

    /// <summary>
    /// 认证成功后分配给当前玩家的英雄席位，范围为 0～3。
    /// 认证失败时不读取该字段。
    /// </summary>
    [NetField(2)]
    public int seatIndex;
}

/// <summary>
/// 一场战斗开始前需要下发给所有参与者的初始数据。
/// </summary>
[BattlePacket(BattlePacketType.Init)]
public struct BattleInitData
{
    /// <summary>
    /// 当前战斗的唯一 ID。
    /// </summary>
    [NetField(1)]
    public long battleID;

    /// <summary>
    /// 本局使用的地图配置 ID。
    /// </summary>
    [NetField(2)]
    public int mapID;

    /// <summary>
    /// 本局确定性随机数的初始种子。
    /// </summary>
    [NetField(3)]
    public int randomSeed;

    /// <summary>
    /// 本局采用的玩法模式。
    /// </summary>
    [NetField(4)]
    public BattleMode mode;

    /// <summary>
    /// 本局四个英雄席位的数据，按 seatIndex 从 0 到 3 排列。
    /// </summary>
    [NetField(5)]
    public BattleSeatData[] seats;

    /// <summary>
    /// 服务端当前使用的战斗配置 Hash。
    /// 客户端进入战斗前用它核对本地配置是否一致。
    /// </summary>
    [NetField(6)]
    public string configHash;

    /// <summary>服务端地图文件的 Hash；客户端开局前核对本地地图。</summary>
    [NetField(7)]
    public string mapHash;
}

/// <summary>
/// 一个英雄席位的开局数据。
/// </summary>
public struct BattleSeatData
{
    /// <summary>
    /// 占用该席位的真人玩家 ID。
    /// 电脑英雄没有账号，固定使用 0，并由 seats 数组下标唯一定位。
    /// </summary>
    [NetField(1)]
    public int playerID;

    /// <summary>
    /// 该英雄所属的阵营。
    /// </summary>
    [NetField(2)]
    public BattleCamp camp;


    /// <summary>本席位脚底的开局 X 坐标，单位为米。</summary>
    [NetField(3)]
    public float spawnX;

    /// <summary>本席位脚底的开局 Z 坐标，单位为米。</summary>
    [NetField(4)]
    public float spawnZ;

    /// <summary>本席位脚底的开局高度，单位为米；不是碰撞体中心高度。</summary>
    [NetField(5)]
    public float spawnY;
}

/// <summary>
/// 玩家在一个目标逻辑帧中请求执行的操作。
/// 每一位代表一种操作，因此同一帧可以同时包含多个操作。
/// </summary>
[Flags]
public enum BattleButtons
{
    None = 0,

    /// <summary>
    /// 使用普通攻击。
    /// </summary>
    Attack = 1 << 0,

    /// <summary>
    /// 使用闪避。
    /// </summary>
    Dodge = 1 << 1,

    /// <summary>
    /// 使用技能一。
    /// </summary>
    Skill1 = 1 << 2,

    /// <summary>
    /// 使用技能二。
    /// </summary>
    Skill2 = 1 << 3,

    /// <summary>
    /// 与资源点、建筑或飞船等战场对象交互。
    /// </summary>
    Interact = 1 << 4,
}

/// <summary>
/// 一个英雄席位在单个逻辑帧中的操作输入。
/// </summary>
public struct BattleInput
{
    /// <summary>
    /// 水平移动输入，范围为 -127～127。
    /// </summary>
    [NetField(1)]
    public int moveX;

    /// <summary>
    /// 前后移动输入，范围为 -127～127。
    /// </summary>
    [NetField(2)]
    public int moveY;

    /// <summary>当前帧是否按住跑步键。</summary>
    [NetField(5)]
    public bool isRunning;

    /// <summary>
    /// 水平面朝向角。0 表示世界前方，0～360 度映射到 0～65535。
    /// </summary>
    [NetField(3)]
    public int aimAngle;

    /// <summary>
    /// 当前目标帧请求执行的攻击、闪避、技能和交互操作。
    /// </summary>
    [NetField(4)]
    public BattleButtons buttons;
}

/// <summary>
/// 客户端通过 KCP 上传的一帧本地操作。
/// </summary>
[BattlePacket(BattlePacketType.PlayerInput)]
public struct PlayerInputPacket
{
    /// <summary>
    /// 该操作应被服务端安排到的逻辑帧。
    /// </summary>
    [NetField(1)]
    public int targetFrame;

    /// <summary>
    /// 当前玩家在目标帧中的量化操作。
    /// </summary>
    [NetField(2)]
    public BattleInput input;
}

/// <summary>服务端最终确定的一帧输入。</summary>
[BattlePacket(BattlePacketType.AuthorityFrame)]
public struct AuthorityFramePacket
{
    /// <summary>服务端确认的逻辑帧编号，从 0 开始连续递增。</summary>
    [NetField(1)]
    public int frame;

    /// <summary>四个席位在这一帧最终执行的输入，数组下标就是席位编号。</summary>
    [NetField(2)]
    public BattleInput[] inputs;
}

/// <summary>通过已认证 KCP 连接上报就绪，身份与战斗由服务端 Session 确定。</summary>
[BattlePacket(BattlePacketType.Ready)]
public struct BattleReadyPacket
{
}

/// <summary>服务端统一开始战斗的通知，先于本局首个操作帧发送。</summary>
[BattlePacket(BattlePacketType.Start)]
public struct BattleStartPacket
{
    [NetField(1)]
    public long battleID;
}
