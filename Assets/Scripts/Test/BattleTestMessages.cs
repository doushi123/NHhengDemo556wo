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

    /// <summary>仅测试入口上传的正式地图 JSON，包含四个手动出生点；正式链路不上传地图。</summary>
    [NetField(4)]
    public string mapJson = string.Empty;
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
