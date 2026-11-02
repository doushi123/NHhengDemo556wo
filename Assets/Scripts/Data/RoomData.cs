/// <summary>
/// 房间从等待成员到正式战斗的阶段，由服务端统一维护。
/// </summary>
public enum RoomState
{
    /// <summary>等待成员加入，房主可以请求开始战斗。</summary>
    Waiting = 0,

    /// <summary>已确定本局成员，正在加载场景和建立 KCP 战斗连接。</summary>
    Loading = 1,

    /// <summary>参与者已完成初始化，服务端开始广播操作帧。</summary>
    InBattle = 2,
}

/// <summary>
/// 服务端下发的完整房间信息；客户端据此显示房主和四个席位。
/// </summary>
public sealed class RoomData
{
    /// <summary>服务端分配的房间号，供其他玩家输入并加入。</summary>
    [NetField(1)]
    public int roomID;

    /// <summary>房主的玩家 ID；权限由服务端根据登录连接校验。</summary>
    [NetField(2)]
    public int ownerPlayerID;

    /// <summary>本房间开战时使用的正式地图 ID。</summary>
    [NetField(3)]
    public int mapID;

    /// <summary>本房间使用的玩法模式，复用现有战斗模式枚举。</summary>
    [NetField(4)]
    public BattleMode mode;

    /// <summary>当前处于等待、加载还是战斗阶段。</summary>
    [NetField(5)]
    public RoomState state;

    /// <summary>
    /// 数组下标就是席位编号，固定四项；0/1 属于阵营 A，2/3 属于阵营 B。
    /// 有人时复用其公开资料，null 表示空位；房间阶段不创建 playerID 为 0 的假玩家。
    /// 开战时才把空位转换为 BattleSeatData 中的电脑席位。
    /// </summary>
    [NetField(6)]
    public PlayerProfileData[] seats = new PlayerProfileData[BattleConst.SeatCount];
}
