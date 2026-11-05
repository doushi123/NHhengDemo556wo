/// <summary>
/// 登录后请求创建房间。只上传玩法模式，由服务端确定对应地图和房主身份。
/// </summary>
[NetMessage(1601)]
public sealed class CreateRoomRequestMsg : BaseMsg
{
    /// <summary>玩家选择的玩法模式；服务端据此确定唯一对应的正式地图。</summary>
    [NetField(1)]
    public BattleMode mode;
}

/// <summary>
/// 通过房间号加入房间。服务端确定当前玩家身份并分配空闲席位。
/// </summary>
[NetMessage(1602)]
public sealed class JoinRoomRequestMsg : BaseMsg
{
    /// <summary>玩家输入的目标房间号。</summary>
    [NetField(1)]
    public int roomID;
}

/// <summary>
/// 离开当前房间；服务端根据登录连接确定玩家及其所在房间。
/// </summary>
[NetMessage(1603)]
public sealed class LeaveRoomRequestMsg : BaseMsg
{
}

/// <summary>
/// 房主请求开始当前房间的战斗；服务端校验房主权限和房间阶段。
/// </summary>
[NetMessage(1604)]
public sealed class StartBattleRequestMsg : BaseMsg
{
}

/// <summary>
/// 创建、加入或房间信息变化后下发的完整数据，客户端用它刷新房间界面。
/// </summary>
[NetMessage(1605)]
public sealed class RoomDataMsg : BaseMsg
{
    /// <summary>服务端确认的当前房间信息。</summary>
    [NetField(1)]
    public RoomData data = new RoomData();
}

/// <summary>
/// 服务端确认当前玩家已离开房间；客户端据此清空房间数据并返回大厅。
/// 剩余成员接收 RoomDataMsg，不接收这条退出确认。
/// </summary>
[NetMessage(1606)]
public sealed class RoomLeftMsg : BaseMsg
{
}

/// <summary>
/// 房主开战后，服务端分别通知本局各真人加载地图并连接 KCP。
/// 每个接收者获得自己的 Token。
/// </summary>
[NetMessage(1607)]
public sealed class BattleEntryMsg : BaseMsg
{
    /// <summary>本次入场的战斗 ID，用于识别就绪、失败和取消属于哪一局。</summary>
    [NetField(3)]
    public long battleID;

    /// <summary>需要加载的正式地图 ID，客户端据此选择战斗场景。</summary>
    [NetField(1)]
    public int mapID;

    /// <summary>服务端为当前接收者签发的战斗凭证，用于现有 KCP 认证。</summary>
    [NetField(2)]
    public string battleToken = string.Empty;
}

/// <summary>请求移动到房间中的指定空席位；玩家身份和当前房间由服务端连接确定。</summary>
[NetMessage(1608)]
public class ChangeSeatRequestMsg : BaseMsg
{
    /// <summary>想要移动到的席位编号，范围为 0～3。</summary>
    [NetField(1)]
    public int seatIndex;
}

/// <summary>客户端无法完成场景或战斗初始化时，通知服务端取消本次入场。</summary>
[NetMessage(1609)]
public class BattleLoadFailedMsg : BaseMsg
{
    [NetField(1)]
    public long battleID;

    /// <summary>加载失败的具体原因。</summary>
    [NetField(2)]
    public string error = string.Empty;
}

/// <summary>本次开战因加载失败、超时或成员离开而取消，客户端关闭加载界面。</summary>
[NetMessage(1610)]
public class BattleCancelledMsg : BaseMsg
{
    [NetField(1)]
    public long battleID;

    /// <summary>供房间成员查看的取消原因。</summary>
    [NetField(2)]
    public string error = string.Empty;
}
