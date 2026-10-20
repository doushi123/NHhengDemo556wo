using System.Collections.Generic;

/// <summary>
/// 管理客户端好友私聊：发送消息、按好友缓存会话，并把实时消息交给已打开的聊天窗口。
/// </summary>
public sealed class PrivateChatManager : AppService
{
    public static PrivateChatManager Instance { get; private set; }
    /// <summary>
    /// 储存和好友之间对话消息的容器
    /// </summary>
    private readonly Dictionary<int, List<PrivateChatData>> conversations =new Dictionary<int, List<PrivateChatData>>();

    protected override void OnInitialize()
    {
        Instance = this;
    }

    public bool Send(int receiverPlayerID, string content)
    {
        content = content.Trim();
        if (content.Length == 0)
        {
            return false;
        }

        if (content.Length > NetConst.MaxChatCount)
        {
            UIManager.Instance.Open(PanelId.Tip, "好友消息最多输入 50 个字符。");
            return false;
        }

        NetManager.Instance.Send(new SendPrivateChatMsg
        {
            receiverPlayerID = receiverPlayerID,
            content = content,
        });
        return true;
    }
    /// <summary>
    /// 得到和好友的对话消息记录
    /// </summary>
    /// <param name="friendPlayerID"></param>
    /// <returns></returns>
    public IReadOnlyList<PrivateChatData> GetConversation(int friendPlayerID)
    {
        if (conversations.TryGetValue(friendPlayerID, out List<PrivateChatData> messages))
        {
            return messages;
        }

        return System.Array.Empty<PrivateChatData>();
    }

    public void ApplyHistory(PrivateChatData[] history)
    {
        conversations.Clear();

        if (history == null)
        {
            return;
        }

        foreach (PrivateChatData data in history)
        {
            AddMessage(data);
        }
    }

    public void Receive(PrivateChatMsg msg)
    {
        if (IsReceived(msg.data))
        {
            return;
        }

        AddMessage(msg.data);

        PrivateChatPanel panel = UIManager.Instance.GetOpenPanel<PrivateChatPanel>(PanelId.Chat);
        if (panel != null && panel.IsChattingWith(GetFriendPlayerID(msg.data)))
        {
            panel.AddMessage(msg.data);
        }
    }

    public void Receive(OperationResultMsg msg)
    {
        if (!msg.success)
        {
            UIManager.Instance.Open(PanelId.Tip, msg.error);
        }
    }

    protected override void OnShutdown()
    {
        conversations.Clear();
        Instance = null;
    }

    private void AddMessage(PrivateChatData data)
    {
        int friendPlayerID = GetFriendPlayerID(data);
        if (!conversations.TryGetValue(friendPlayerID, out List<PrivateChatData> messages))
        {
            messages = new List<PrivateChatData>();
            conversations.Add(friendPlayerID, messages);
        }

        messages.Add(data);
        if (messages.Count > NetConst.MaxPrivateChatCount)
        {
            messages.RemoveAt(0);
        }
    }

    private bool IsReceived(PrivateChatData data)
    {
        int friendPlayerID = GetFriendPlayerID(data);
        if (!conversations.TryGetValue(friendPlayerID, out List<PrivateChatData> messages))
        {
            return false;
        }

        foreach (PrivateChatData message in messages)
        {
            if (message.messageID == data.messageID)
            {
                return true;
            }
        }

        return false;
    }

    private int GetFriendPlayerID(PrivateChatData data)
    {
        int currentPlayerID = PlayerDataManager.Instance.CurrentPlayerData.playerID;
        return data.senderPlayerID == currentPlayerID? data.receiverPlayerID: data.senderPlayerID;
    }
}
