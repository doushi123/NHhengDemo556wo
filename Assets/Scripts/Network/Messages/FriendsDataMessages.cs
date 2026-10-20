/// <summary>
/// 请求当前登录账号自己的最新好友数据。身份由服务端连接确定。
/// </summary>
[NetMessage(1101)]
public sealed class RequestFriendsDataMsg : BaseMsg
{
}

/// <summary>
/// 当前玩家的最新好友列表和待处理申请。
/// </summary>
[NetMessage(1102)]
public sealed class FriendsDataMsg : BaseMsg
{
    [NetField(1)]
    public PlayerSocialData data;
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
    /// <summary>
    /// 请求玩家数据
    /// </summary>
    RequestPlayerData,
    /// <summary>
    /// 请求好友数据
    /// </summary>
    RequestFriendsData,
    /// <summary>
    /// 查找玩家
    /// </summary>
    SearchPlayer,
    /// <summary>
    /// 发送好友请求
    /// </summary>
    SendFriendRequest,
    /// <summary>
    /// 处理好友请求
    /// </summary>
    HandleFriendRequest,
    /// <summary>
    /// 登录
    /// </summary>
    Login,
    /// <summary>
    /// 注册
    /// </summary>
    Register,
    SendPrivateChat,
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
/// <summary>
/// 发送好友申请的消息类
/// </summary>
[NetMessage(1105)]
public sealed class SendFriendRequestMsg : BaseMsg
{
    [NetField(1)]
    public int targetPlayerID;
}

/// <summary>
/// 同意或拒绝指定玩家发来的好友申请。
/// </summary>
[NetMessage(1106)]
public sealed class HandleFriendRequestMsg : BaseMsg
{
    /// <summary>
    /// 申请发送者的玩家 ID。
    /// </summary>
    [NetField(1)]
    public int senderPlayerID;
    /// <summary>
    /// 是否同意好友申请
    /// </summary>
    [NetField(2)]
    public bool accept;
}
