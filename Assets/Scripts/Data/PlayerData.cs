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
    [NetField(1)]
    public int playerID;

    [NetField(2)]
    public string account = string.Empty;

    [NetField(3)]
    public PlayerOnlineState state;
    /// <summary>
    /// 等级
    /// </summary>
    [NetField(4)]
    public int level;
}
