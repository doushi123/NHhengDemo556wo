/// <summary>
/// 客户端请求向指定好友发送私聊。发送者身份和时间由服务端确定。
/// </summary>
[NetMessage(1401)]
public sealed class SendPrivateChatMsg : BaseMsg
{
    [NetField(1)]
    public int receiverPlayerID;

    [NetField(2)]
    public string content = string.Empty;
}

/// <summary>
/// 服务端确认后推送给发送者和接收者的私聊消息。
/// </summary>
[NetMessage(1402)]
public sealed class PrivateChatMsg : BaseMsg
{
    [NetField(1)]
    public PrivateChatData data;
}
