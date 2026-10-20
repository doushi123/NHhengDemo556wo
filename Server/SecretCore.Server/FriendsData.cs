using System.Text.Json.Serialization;

/// <summary>
/// 当前玩家自己保存的一条好友关系。
/// 备注和特别关注只属于当前玩家，不是对方的公开资料。
/// 稳定关系写入 JSON，playerData 只在网络下发前动态填充。
/// </summary>
public class FriendRelationData
{
    /// <summary>
    /// 好友唯一的玩家 ID。
    /// </summary>
    [NetField(1)]
    public int friendPlayerID;

    /// <summary>
    /// 当前玩家给该好友设置的备注名，仅自己可见。
    /// </summary>
    [NetField(2)]
    public string remark = string.Empty;

    /// <summary>
    /// 是否是特别关注。
    /// </summary>
    [NetField(3)]
    public bool isFavorite;

    /// <summary>
    /// 根据 friendPlayerID 查询的最新公开资料，只用于网络下发，不写入 JSON。
    /// </summary>
    [JsonIgnore]
    [NetField(4)]
    public PlayerProfileData? playerData;
}

/// <summary>
/// 一条好友申请。
/// 当前只保存尚未处理的申请，senderData 不写入 JSON，只用于网络下发。
/// </summary>
public class FriendRequestData
{
    /// <summary>
    /// 发送申请的玩家 ID。
    /// </summary>
    [NetField(1)]
    public int senderPlayerID;

    /// <summary>
    /// 申请创建时间。
    /// </summary>
    [NetField(2)]
    public long createTime;

    /// <summary>
    /// 根据 senderPlayerID 查询的最新公开资料，只用于网络下发，不写入 JSON。
    /// </summary>
    [JsonIgnore]
    [NetField(3)]
    public PlayerProfileData? senderData;
}

/// <summary>
/// 一个玩家需要持久化的最小社交数据。
/// 好友的公开资料不重复永久保存，由服务端根据玩家 ID 填入最新资料。
/// </summary>
public class PlayerSocialData
{
    /// <summary>
    /// 该玩家的好友。
    /// </summary>
    [NetField(1)]
    public FriendRelationData[] friends = Array.Empty<FriendRelationData>();

    /// <summary>
    /// 该玩家尚未处理的好友申请。
    /// </summary>
    [NetField(2)]
    public FriendRequestData[] friendRequests = Array.Empty<FriendRequestData>();
}
