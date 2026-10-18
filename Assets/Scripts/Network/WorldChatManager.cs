using System.Collections.Generic;

/// <summary>
/// 管理客户端的世界聊天业务：发送校验、历史缓存、消息去重和数量限制。
/// 网络连接和消息收发仍由 NetManager 负责。
/// </summary>
public sealed class WorldChatManager : AppService
{
    public static WorldChatManager Instance { get; private set; }
    /// <summary>
    /// 接收到的世界消息列表
    /// </summary>
    private readonly List<WorldChatData> messages = new List<WorldChatData>();
    /// <summary>
    /// 外部只能读取
    /// </summary>
    public IReadOnlyList<WorldChatData> Messages => messages;

    protected override void OnInitialize()
    {
        Instance = this;
    }

    public bool Send(string content)
    {
        content = content.Trim();
        if (content.Length == 0)
        {
            return false;
        }

        if (content.Length > NetConst.MaxChatCount)
        {
            UIManager.Instance.Open(PanelId.Tip, "世界消息最多输入 50 个字符。");
            return false;
        }

        NetManager.Instance.Send(new SendWorldChatMsg { content = content });
        return true;
    }

    /// <summary>
    /// 用服务端下发的历史替换本地缓存。
    /// </summary>
    public void ApplyHistory(WorldChatData[] history)
    {
        messages.Clear();

        if (history == null || history.Length == 0)
        {
            return;
        }

        foreach (WorldChatData data in history)
        {
            AddMessage(data);
        }
    }

    /// <summary>
    /// 接收服务端广播的世界消息。
    /// </summary>
    public void Receive(WorldChatMsg msg)
    {
        if (!msg.success)
        {
            UIManager.Instance.Open(PanelId.Tip, msg.error);
            return;
        }

        // 登录历史和紧接着到达的广播可能包含同一条消息，因此按消息 ID 去重。
        if (IsReceived(msg.data.messageID))
        {
            return;
        }

        AddMessage(msg.data);

        // 大厅关闭时只保存数据；大厅打开时，再直接追加对应的消息 UI。
        LobbyPanel lobbyPanel = UIManager.Instance.GetOpenPanel<LobbyPanel>(PanelId.Lobby);
        if (lobbyPanel != null)
        {
            lobbyPanel.AddWorldChatMessage(msg.data);
        }
    }

    protected override void OnShutdown()
    {
        messages.Clear();
        Instance = null;
    }

    private void AddMessage(WorldChatData data)
    {
        messages.Add(data);
        if (messages.Count > NetConst.MaxWorldChatCount)
        {
            messages.RemoveAt(0);       //世界聊天频道消息最多保存MaxWorldChatCount条，多的就把老的移除
        }
    }

    private bool IsReceived(long messageID)
    {
        foreach (WorldChatData data in messages)
        {
            if (data.messageID == messageID)
            {
                return true;
            }
        }

        return false;
    }
}
