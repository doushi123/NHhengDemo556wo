/// <summary>
/// 请求当前登录账号自己的最新好友数据。身份由服务端连接确定。
/// </summary>
[NetMessage(1101)]
public sealed class RequestFriendsDataMsg : BaseMsg
{
}

/// <summary>
/// 服务端返回好友列表和收到的待处理好友申请。
/// </summary>
[NetMessage(1102)]
public sealed class FriendsDataMsg : BaseMsg
{
    [NetField(1)]
    public PlayerSocialData? data;
}

/// <summary>
/// 按唯一账号名精确搜索玩家，成功结果复用 PlayerDataMsg。
/// </summary>
[NetMessage(1103)]
public sealed class SearchPlayerRequestMsg : BaseMsg
{
    [NetField(1)]
    public string account = string.Empty;
}

public enum OperationType
{
    RequestPlayerData,
    RequestFriendsData,
    SearchPlayer,
    SendFriendRequest,
    HandleFriendRequest,
    Login,
    Register,
}

/// <summary>
/// 没有额外返回数据的操作结果，以及数据请求失败时的统一错误结果。
/// </summary>
[NetMessage(1104)]
public sealed class OperationResultMsg : BaseMsg
{
    [NetField(1)]
    public OperationType operation;

    [NetField(2)]
    public bool success;

    [NetField(3)]
    public string error = string.Empty;
}

[NetMessage(1105)]
public sealed class SendFriendRequestMsg : BaseMsg
{
    [NetField(1)]
    public int targetPlayerID;

    [NetField(2)]
    public string message = string.Empty;
}

/// <summary>
/// 同意或拒绝指定玩家发来的好友申请。
/// </summary>
[NetMessage(1106)]
public sealed class HandleFriendRequestMsg : BaseMsg
{
    /// <summary>
    /// 申请发送者的玩家 ID。服务端结合当前登录的接收者定位申请。
    /// </summary>
    [NetField(1)]
    public int senderPlayerID;

    [NetField(2)]
    public bool accept;
}
