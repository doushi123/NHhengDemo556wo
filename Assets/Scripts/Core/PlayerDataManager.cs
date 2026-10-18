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

    protected override void OnInitialize()
    {
        Instance = this;
    }

    public void RequestPlayerData(int playerID)
    {
        NetManager.Instance.Send(new RequestPlayerDataMsg { playerID = playerID });
    }

    public void RequestFriendsData(int playerID)
    {
        NetManager.Instance.Send(new RequestFriendsDataMsg { playerID = playerID });
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
        if (!msg.success)
        {
            CurrentPlayerData = null;
            UIManager.Instance.Open(PanelId.Tip, msg.error);
            return;
        }

        CurrentPlayerData = msg.data;
        Debug.Log($"收到玩家资料：{msg.data.account}，等级 {msg.data.level}");
    }

    public void Receive(FriendsDataMsg msg)
    {
        if (!msg.success)
        {
            CurrentFriendsData = null;
            UIManager.Instance.Open(PanelId.Tip, msg.error);
            return;
        }

        CurrentFriendsData = msg.data;
        Debug.Log($"收到好友数据：{msg.data.friends.Length} 名好友，{msg.data.friendRequests.Length} 条申请");
    }

    protected override void OnShutdown()
    {
        CurrentPlayerData = null;
        CurrentFriendsData = null;
        Instance = null;
    }
}
