using System;
using kcp2k;
using UnityEngine;

/// <summary>
/// 管理客户端与战斗服务端之间的 KCP 连接。
/// 负责战斗认证、接收初始化数据和上传本地操作。
/// </summary>
public sealed class BattleNetManager : AppService
{
    public static BattleNetManager Instance { get; private set; }

    private KcpClient client;
    private string battleToken;
    /// <summary>
    /// 是否应该关闭KCP连接
    /// </summary>
    private bool shouldClose;
    /// <summary>
    /// 当前是否通过认证
    /// </summary>
    public bool IsAuthenticated { get; private set; }
    /// <summary>
    /// 当前客户端控制四个英雄席位中的哪一个
    /// </summary>
    public int SeatIndex { get; private set; }
    public BattleInitData?  InitData { get; private set; }

    protected override void OnInitialize()
    {
        Instance = this;
    }

    private void Update()
    {
        if (client == null)
        {
            return;
        }
        //处理从服务端发到客户端的网络数据
        client.TickIncoming();
        //让KCP处理当前等待发送、确认或重传的数据
        client.TickOutgoing();
        if (shouldClose)
        {
            Close();
        }
    }

    /// <summary>
    /// 使用 TCP 服务端签发的临时 Token 建立战斗连接。
    /// </summary>
    public void Connect(string token)
    {
        Close();

        battleToken = token;
        shouldClose = false;
        client = new KcpClient(OnConnected, OnData, OnDisconnected, OnError, CreateConfig());
        client.Connect(NetConst.ServerIP, NetConst.BattlePort);
    }

    /// <summary>
    /// 上传一帧量化后的本地操作。
    /// </summary>
    public void SendInput(PlayerInputPacket packet)
    {
        if (!IsAuthenticated || client == null)
        {
            return;
        }

        Send(BattlePacketCodec.Serialize(packet));
    }

    public void Close()
    {
        KcpClient currentClient = client;
        client = null;
        currentClient?.Disconnect();

        battleToken = null;
        shouldClose = false;
        IsAuthenticated = false;
        SeatIndex = 0;
        InitData = null;
    }

    protected override void OnShutdown()
    {
        Close();
        Instance = null;
    }

    private void OnConnected()
    {
        Send(BattlePacketCodec.Serialize(new BattleAuthPacket
        {
            battleToken = battleToken,
        }));
    }

    private void OnData(ArraySegment<byte> bytes, KcpChannel channel)
    {
        if (channel != KcpChannel.Reliable)
        {
            return;
        }

        try
        {
            BattlePacketType type = BattlePacketCodec.GetPacketType(bytes);
            switch (type)
            {
                case BattlePacketType.AuthResult:
                    ReceiveAuthResult(
                        BattlePacketCodec.Deserialize<BattleAuthResultPacket>(bytes));
                    break;

                case BattlePacketType.Init:
                    InitData = BattlePacketCodec.Deserialize<BattleInitData>(bytes);
                    break;

                default:
                    Debug.LogWarning("客户端未处理的战斗消息：" + type);
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("战斗消息解析失败：" + exception.Message);
            UIManager.Instance.Open(PanelId.Tip, "收到的战斗数据异常。");
            shouldClose = true;
        }
    }

    private void ReceiveAuthResult(BattleAuthResultPacket packet)
    {
        if (packet.result != BattleAuthResult.Success)
        {
            UIManager.Instance.Open(PanelId.Tip, "战斗不存在或已经结束。");
            shouldClose = true;
            return;
        }

        IsAuthenticated = true;
        SeatIndex = packet.seatIndex;
    }

    private void OnDisconnected()
    {
        shouldClose = true;
    }

    private void OnError(ErrorCode error, string message)
    {
        Debug.LogWarning("KCP 战斗连接错误：" + error + "，" + message);
        UIManager.Instance.Open(PanelId.Tip, "战斗连接发生错误。");
        shouldClose = true;
    }

    private void Send(byte[] bytes)
    {
        client.Send(new ArraySegment<byte>(bytes), KcpChannel.Reliable);
    }

    private static KcpConfig CreateConfig()
    {
        return new KcpConfig(
            DualMode: false,
            NoDelay: true,
            Interval: 10,
            FastResend: 2,
            CongestionWindow: false,
            SendWindowSize: 32,
            ReceiveWindowSize: 32,
            Timeout: 10000);
    }
}
