/// <summary>
/// 玩家当前对外显示的在线状态。
/// </summary>
public enum PlayerOnlineState
{
    /// <summary>
    /// 离线
    /// </summary>
    Offline,
    /// <summary>
    /// 在线，在大厅中
    /// </summary>
    Lobby,
    /// <summary>
    /// 在队伍中
    /// </summary>
    InTeam,
    /// <summary>
    /// 在对局中
    /// </summary>
    InBattle,
}

/// <summary>
/// 玩家自己的公开资料。
/// </summary>
public class PlayerProfileData
{
    /// <summary>
    /// 玩家唯一 ID。
    /// </summary>
    [NetField(1)]
    public int playerID;

    /// <summary>
    /// 账户名，同时作为当前玩家对外显示的名称。
    /// </summary>
    [NetField(2)]
    public string account = string.Empty;

    /// <summary>
    /// 玩家当前所在的在线状态。
    /// </summary>
    [NetField(3)]
    public PlayerOnlineState state;

    /// <summary>
    /// 玩家当前等级。
    /// </summary>
    [NetField(4)]
    public int level;
}
