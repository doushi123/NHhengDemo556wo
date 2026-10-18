/// <summary>
/// 客户端请求某个玩家自己的好友数据。
/// 当前还没有登录 Session，所以暂时由请求携带 playerID。
/// </summary>
[NetMessage(1101)]
public sealed class RequestFriendsDataMsg : BaseMsg
{
    [NetField(1)]
    public int playerID;
}

/// <summary>
/// 服务端返回好友列表和收到的好友申请。
/// </summary>
[NetMessage(1102)]
public sealed class FriendsDataMsg : BaseMsg
{
    [NetField(1)]
    public bool success;

    [NetField(2)]
    public string error = string.Empty;

    [NetField(3)]
    public PlayerSocialData? data;
}
