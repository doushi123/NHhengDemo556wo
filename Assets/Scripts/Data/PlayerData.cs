/// <summary>
/// 玩家当前对外显示的在线状态。
/// </summary>
public enum PlayerOnlineState
{
    Offline,
    Lobby,
    InTeam,
    InBattle,
}

/// <summary>
/// 玩家自己的公开资料。
/// 登录账号和密码信息不属于玩家资料。
/// </summary>
public class PlayerProfileData
{
    [NetField(1)]
    public int playerID;

    [NetField(2)]
    public string account = string.Empty;

    [NetField(3)]
    public PlayerOnlineState state;

    [NetField(4)]
    public int level;
}
