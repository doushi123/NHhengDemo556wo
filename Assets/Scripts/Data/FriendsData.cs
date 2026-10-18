using System;

/// <summary>
/// 当前玩家自己保存的一条好友关系。
/// 备注和特别关注只属于当前玩家，不是对方的公开资料。
/// </summary>
public class FriendRelationData
{
    [NetField(1)]
    public int friendPlayerID;

    [NetField(2)]
    public string remark = string.Empty;

    [NetField(3)]
    public bool isFavorite;
}

public enum FriendRequestState
{
    Pending,
    Accepted,
    Rejected,
}

/// <summary>
/// 一条好友申请。
/// </summary>
public class FriendRequestData
{
    [NetField(1)]
    public long requestID;

    [NetField(2)]
    public int senderPlayerID;

    [NetField(3)]
    public int receiverPlayerID;

    [NetField(4)]
    public string message = string.Empty;

    [NetField(5)]
    public FriendRequestState state;

    [NetField(6)]
    public long createTime;
}

/// <summary>
/// 一个玩家需要持久化的最小社交数据。
/// 好友的用户名和在线状态不重复保存在这里，需要时读取对方最新资料。
/// </summary>
public class PlayerSocialData
{
    [NetField(1)]
    public FriendRelationData[] friends = Array.Empty<FriendRelationData>();

    [NetField(2)]
    public FriendRequestData[] friendRequests = Array.Empty<FriendRequestData>();
}
