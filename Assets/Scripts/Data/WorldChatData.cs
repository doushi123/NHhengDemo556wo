/// <summary>
/// 服务端确认并广播的一条世界聊天消息。
/// </summary>
public class WorldChatData
{
    [NetField(1)]
    public long messageID;

    [NetField(2)]
    public int senderPlayerID;

    [NetField(3)]
    public string senderAccount = string.Empty;

    [NetField(4)]
    public string content = string.Empty;

    [NetField(5)]
    public long sendTime;
}
