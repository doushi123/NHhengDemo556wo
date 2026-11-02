/// <summary>客户端和服务端共用的测试模式开关。</summary>
public static class BattleTestMode
{
    /// <summary>改成 true 后，重新编译客户端和服务端才能启动测试战斗。</summary>
    public const bool Enabled = true;
}

/// <summary>
/// 仅供 Unity 战斗测试脚本使用的测试战斗请求。
/// 正式登录和房间流程不会发送这条消息。
/// </summary>
[NetMessage(1501)]
public sealed class BattleTestRequestMsg : BaseMsg
{
    [NetField(1)]
    public int mapID;

    [NetField(2)]
    public float spawnX;

    [NetField(3)]
    public float spawnZ;

    /// <summary>仅供测试链路上传的正式导出地图 JSON，去掉缩进以减小消息体；正式链路不上传地图。</summary>
    [NetField(4)]
    public string mapJson = string.Empty;

    /// <summary>场景中玩家根节点（脚底）的高度，单位为米。</summary>
    [NetField(5)]
    public float spawnY;
}

/// <summary>
/// 服务端返回测试战斗的临时 Token；Token 为空时 error 保存失败原因。
/// </summary>
[NetMessage(1502)]
public sealed class BattleTestTokenMsg : BaseMsg
{
    [NetField(1)]
    public string battleToken = string.Empty;

    [NetField(2)]
    public string error = string.Empty;
}
