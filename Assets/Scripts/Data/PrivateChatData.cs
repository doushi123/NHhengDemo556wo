/// <summary>
/// 一条好友间的私聊消息。
/// </summary>
public class PrivateChatData
{
    [NetField(1)]
    public long messageID;

    [NetField(2)]
    public int senderPlayerID;

    [NetField(3)]
    public int receiverPlayerID;

    [NetField(4)]
    public string content = string.Empty;

    /// <summary>
    /// 服务端生成的 Unix 时间戳。客户端显示为 yyyy.M.d  HH:mm。
    /// </summary>
    [NetField(5)]
    public long sendTime;
}
