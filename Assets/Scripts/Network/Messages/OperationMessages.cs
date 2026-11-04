/// <summary>
/// 操作类型，例如登录、发送好友申请、创建房间。
/// 服务端返回结果时带上它，让客户端知道是哪项操作成功或失败。
/// </summary>
public enum OperationType
{
    /// <summary>请求玩家公开资料。</summary>
    RequestPlayerData,

    /// <summary>请求当前账号的好友数据。</summary>
    RequestFriendsData,

    /// <summary>按账号搜索玩家。</summary>
    SearchPlayer,

    /// <summary>发送好友申请。</summary>
    SendFriendRequest,

    /// <summary>同意或拒绝好友申请。</summary>
    HandleFriendRequest,

    /// <summary>登录账号。</summary>
    Login,

    /// <summary>注册账号。</summary>
    Register,

    /// <summary>向好友发送私聊。</summary>
    SendPrivateChat,

    /// <summary>创建房间。</summary>
    CreateRoom,

    /// <summary>通过房间号加入房间。</summary>
    JoinRoom,

    /// <summary>离开当前房间。</summary>
    LeaveRoom,

    /// <summary>房主请求开始战斗。</summary>
    StartBattle,

    /// <summary>切换到房间中的空席位。</summary>
    ChangeSeat,
}

/// <summary>
/// 没有额外返回数据的操作结果，以及数据请求失败时的统一错误结果。
/// </summary>
[NetMessage(1104)]
public sealed class OperationResultMsg : BaseMsg
{
    /// <summary>本结果回复的请求类型。</summary>
    [NetField(1)]
    public OperationType operation;

    /// <summary>该请求是否成功执行。</summary>
    [NetField(2)]
    public bool success;

    /// <summary>失败时供客户端显示的原因。</summary>
    [NetField(3)]
    public string error = string.Empty;
}
