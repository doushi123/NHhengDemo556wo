/// <summary>
/// 服务端确认并保存的一条好友私聊消息。
/// </summary>
public class PrivateChatData
{
    /// <summary>
    /// 服务端为消息分配的唯一 ID。
    /// </summary>
    [NetField(1)]
    public long messageID;

    /// <summary>
    /// 发送者的玩家 ID。
    /// </summary>
    [NetField(2)]
    public int senderPlayerID;

    /// <summary>
    /// 接收者的玩家 ID。
    /// </summary>
    [NetField(3)]
    public int receiverPlayerID;

    /// <summary>
    /// 私聊消息正文。
    /// </summary>
    [NetField(4)]
    public string content = string.Empty;

    /// <summary>
    /// 服务端生成的 Unix 时间戳。客户端显示为 yyyy.M.d  HH:mm。
    /// </summary>
    [NetField(5)]
    public long sendTime;
}
