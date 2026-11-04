/// <summary>
/// 保存服务端确认的房间数据，并通过现有 TCP 通道发起房间操作。
/// 创建、加入、退出都以服务端响应为准，不提前修改本地席位。
/// </summary>
public sealed class RoomManager : AppService
{
    public static RoomManager Instance { get; private set; }

    /// <summary>服务端最近下发的完整房间数据。</summary>
    public RoomData CurrentRoom { get; private set; }

    /// <summary>创建或加入请求正在等待响应，避免房间数据尚未返回时重复发送入房请求。</summary>
    private bool entering;

    protected override void OnInitialize()
    {
        Instance = this;
    }

    /// <summary>只上传所选玩法模式。</summary>
    public void CreateRoom(BattleMode mode)
    {
        if (!CanEnter())
        {
            return;
        }

        // 未连接时由 NetManager 显示提示，不进入等待响应的状态。
        entering = NetManager.Instance.IsConnected;
        NetManager.Instance.Send(new CreateRoomRequestMsg { mode = mode });
    }

    /// <summary>通过房间号加入，席位由服务端分配。</summary>
    public void JoinRoom(int roomID)
    {
        if (!CanEnter())
        {
            return;
        }

        entering = NetManager.Instance.IsConnected;
        NetManager.Instance.Send(new JoinRoomRequestMsg { roomID = roomID });
    }

    /// <summary>在客户端提示先退出当前房间，或等待正在处理的入房请求。</summary>
    private bool CanEnter()
    {
        if (CurrentRoom != null)
        {
            UIManager.Instance.Open(PanelId.Tip, "请先离开当前房间。");
            return false;
        }
        if (entering)
        {
            UIManager.Instance.Open(PanelId.Tip, "正在进入房间，请稍候。");
            return false;
        }
        return true;
    }

    /// <summary>请求离开当前房间，收到退出确认后才清空本地数据。</summary>
    public void LeaveRoom()
    {
        NetManager.Instance.Send(new LeaveRoomRequestMsg());
    }

    /// <summary>创建、加入或其他成员变化后，替换本地房间数据。</summary>
    public void Receive(RoomDataMsg msg)
    {
        CurrentRoom = msg.data;
        entering = false;
    }

    /// <summary>收到服务端退出确认，清空当前房间。</summary>
    public void Receive(RoomLeftMsg msg)
    {
        Clear();
    }

    /// <summary>房间操作失败时显示服务端原因，保留原来已经确认的房间数据。</summary>
    public void Receive(OperationResultMsg msg)
    {
        if (msg.operation == OperationType.CreateRoom || msg.operation == OperationType.JoinRoom)
        {
            entering = false;
        }
        if (!msg.success)
        {
            UIManager.Instance.Open(PanelId.Tip, msg.error);
        }
    }

    /// <summary>主动退出或 TCP 断线时清空房间，由主线程调用。</summary>
    public void Clear()
    {
        CurrentRoom = null;
        entering = false;
    }

    /// <summary>关闭服务时清空缓存并释放静态入口。</summary>
    protected override void OnShutdown()
    {
        Clear();
        Instance = null;
    }
}
