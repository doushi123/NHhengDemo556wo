/// <summary>
/// 服务端确认并广播的一条世界聊天消息。
/// </summary>
public class WorldChatData
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
    /// 发送者的账户名，用于客户端显示。
    /// </summary>
    [NetField(3)]
    public string senderAccount = string.Empty;

    /// <summary>
    /// 消息正文。
    /// </summary>
    [NetField(4)]
    public string content = string.Empty;

    /// <summary>
    /// 服务端生成的 Unix 时间戳。
    /// </summary>
    [NetField(5)]
    public long sendTime;
}
