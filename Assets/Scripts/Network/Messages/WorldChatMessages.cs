/// <summary>
/// 客户端只上传聊天正文，发送者身份和时间由服务端填写。
/// </summary>
[NetMessage(1301)]
public sealed class SendWorldChatMsg : BaseMsg
{
    [NetField(1)]
    public string content = string.Empty;
}

/// <summary>
/// 服务端广播的正式世界聊天消息，也用于向发送者返回失败原因。
/// </summary>
[NetMessage(1302)]
public sealed class WorldChatMsg : BaseMsg
{
    [NetField(1)]
    public bool success;

    [NetField(2)]
    public string error = string.Empty;

    [NetField(3)]
    public WorldChatData data;
}
