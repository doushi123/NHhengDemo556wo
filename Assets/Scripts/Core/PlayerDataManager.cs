using UnityEngine;

/// <summary>
/// 保存当前玩家资料和好友数据，并处理这两类数据的请求与响应。
/// 网络连接和消息收发仍由 NetManager 负责。
/// </summary>
public class PlayerDataManager : AppService
{
    public static PlayerDataManager Instance { get; private set; }

    public PlayerProfileData CurrentPlayerData { get; private set; }
    public PlayerSocialData CurrentFriendsData { get; private set; }
    public PlayerProfileData SearchResult { get; private set; }

    protected override void OnInitialize()
    {
        Instance = this;
    }

    public void RequestPlayerData(int playerID)
    {
        NetManager.Instance.Send(new RequestPlayerDataMsg { playerID = playerID });
    }

    public void RequestFriendsData()
    {
        NetManager.Instance.Send(new RequestFriendsDataMsg());
    }

    public void SearchPlayer(string account)
    {
        SearchResult = null;
        NetManager.Instance.Send(new SearchPlayerRequestMsg { account = account.Trim() });
    }

    public void SendFriendRequest(int targetPlayerID, string message)
    {
        NetManager.Instance.Send(new SendFriendRequestMsg
        {
            targetPlayerID = targetPlayerID,
            message = message.Trim(),
        });
    }

    public void HandleFriendRequest(int senderPlayerID, bool accept)
    {
        NetManager.Instance.Send(new HandleFriendRequestMsg
        {
            senderPlayerID = senderPlayerID,
            accept = accept,
        });
    }

    /// <summary>
    /// 保存登录或注册成功时服务端统一下发的玩家数据。
    /// </summary>
    public void ApplyLoginData(PlayerProfileData playerData, PlayerSocialData socialData)
    {
        CurrentPlayerData = playerData;
        CurrentFriendsData = socialData;
    }

    public void Receive(PlayerDataMsg msg)
    {
        SearchResult = msg.data;
        Debug.Log($"收到玩家资料：{msg.data.account}，等级 {msg.data.level}");
    }

    public void Receive(FriendsDataMsg msg)
    {
        CurrentFriendsData = msg.data;
        Debug.Log($"收到好友数据：{msg.data.friends.Length} 名好友，{msg.data.friendRequests.Length} 条申请");
    }

    public void Receive(OperationResultMsg msg)
    {
        if (msg.operation == OperationType.SearchPlayer
            || msg.operation == OperationType.RequestPlayerData)
        {
            SearchResult = null;
        }

        if (!msg.success)
        {
            UIManager.Instance.Open(PanelId.Tip, msg.error);
            return;
        }

        if (msg.operation == OperationType.SendFriendRequest)
        {
            UIManager.Instance.Open(PanelId.Tip, "好友申请已发送。");
        }
    }

    protected override void OnShutdown()
    {
        CurrentPlayerData = null;
        CurrentFriendsData = null;
        SearchResult = null;
        Instance = null;
    }
}
