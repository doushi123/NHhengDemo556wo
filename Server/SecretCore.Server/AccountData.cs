public class AccountData
{
    /// <summary>
    /// 账户名  
    /// </summary>
    public string account;
    public string passwordHash;
    public string passwordSalt;
    /// <summary>
    /// 账号创建时间
    /// </summary>
    public long createTime;
    /// <summary>
    /// 最后登录时间
    /// </summary>
    public long lastLoginTime;
    public PlayerProfileData playerData = new PlayerProfileData();
}

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
    /// 在游戏中
    /// </summary>
    InBattle,
}

/// <summary>
/// 当前账号对应的玩家资料。
/// </summary>
public class PlayerProfileData
{
    [NetField(1)]
    public int playerID;

    /// <summary>
    /// 账户名，同时作为玩家对外显示的用户名。
    /// </summary>
    [NetField(2)]
    public string account = string.Empty;

    /// <summary>
    /// 玩家的在线状态
    /// </summary>
    [NetField(3)]
    public PlayerOnlineState state;
    /// <summary>
    /// 玩家当前等级
    /// </summary>
    [NetField(4)]
    public int level;
}
