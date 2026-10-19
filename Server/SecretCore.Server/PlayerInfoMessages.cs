/// <summary>
/// 客户端请求某个玩家的公开资料。
/// </summary>
[NetMessage(1001)]
public sealed class RequestPlayerDataMsg : BaseMsg
{
    [NetField(1)]
    public int playerID;
}

/// <summary>
/// 服务端返回的玩家公开资料。
/// </summary>
[NetMessage(1002)]
public sealed class PlayerDataMsg : BaseMsg
{
    [NetField(1)]
    public PlayerProfileData? data;
}
