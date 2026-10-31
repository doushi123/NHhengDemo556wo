using System.Collections.Concurrent;
using System.Security.Cryptography;
using kcp2k;

/// <summary>
/// 一条已经通过认证的玩家操作，供后续战斗房间按目标帧收集。
/// </summary>
public readonly struct BattleClientInput
{
    /// <summary>
    /// 这条操作所属的战斗。
    /// </summary>
    public readonly long battleID;

    /// <summary>
    /// 发送这条操作的英雄席位。
    /// </summary>
    public readonly int seatIndex;

    /// <summary>
    /// 客户端上传的目标帧和具体操作。
    /// </summary>
    public readonly PlayerInputPacket packet;

    public BattleClientInput(long battleID, int seatIndex, PlayerInputPacket packet)
    {
        this.battleID = battleID;
        this.seatIndex = seatIndex;
        this.packet = packet;
    }
}

/// <summary>
/// 独立 C# 服务端中的 KCP 战斗通道。
/// 负责 Token 认证与输入接收。
/// </summary>
public sealed class BattleKcpServer
{
    /// <summary>
    /// Token 对应的一次性入场信息。认证后即从 tickets 中删除。
    /// </summary>
    private sealed class BattleTicket
    {
        public BattleInitData initData;
        public int seatIndex;
        public DateTime expireTime;
    }

    /// <summary>
    /// 已认证连接的战斗身份，供后续玩家输入确定战斗和席位。
    /// </summary>
    private sealed class BattleSession
    {
        public BattleInitData initData;
        public int seatIndex;
    }

    
    /// <summary>
    /// 保存尚未使用的临时战斗凭证。
    /// 房间系统创建 Token 后写入，KCP 客户端认证成功时取出并删除。
    /// </summary>
    private readonly ConcurrentDictionary<string, BattleTicket> tickets = new();

    /// <summary>
    /// 保存已通过认证的 KCP 连接及其战斗身份。
    /// 收到玩家操作时，根据 connectionID 确认它属于哪场战斗和哪个席位。
    /// </summary>
    private readonly Dictionary<int, BattleSession> sessions = new();

    /// <summary>
    /// 已完成认证和解码的玩家操作队列。
    /// KCP 网络线程写入，后续战斗帧调度线程从中取出操作。
    /// </summary>
    private readonly ConcurrentQueue<BattleClientInput> inputQueue = new();

    /// <summary>
    /// kcp2k 提供的服务端对象，负责监听连接和实际收发数据。
    /// </summary>
    private KcpServer? server;

    /// <summary>
    /// 持续调用 KCP 收发更新的网络线程。
    /// </summary>
    private Thread? thread;

    /// <summary>
    /// 控制 KCP 网络线程是否继续运行。
    /// </summary>
    private volatile bool isRunning;

    /// <summary>
    ///创建Kcp2k服务端。
    /// </summary>
    public void Start(ushort port)
    {
        if (isRunning)
        {
            return;
        }

        server = new KcpServer(OnConnected, OnData, OnDisconnected, OnError, CreateConfig());
        server.Start(port);

        isRunning = true;
        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "BattleKcpServer",
        };
        thread.Start();

        Console.WriteLine("正在监听 KCP 战斗端口：" + port);
    }

    /// <summary>
    /// 为已通过 TCP 登录且已分配战斗席位的玩家签发一次性 Token。
    /// 后续房间模块创建战斗时调用。
    /// </summary>
    public string CreateToken(BattleInitData initData, int seatIndex)
    {
        if (initData.seats == null|| initData.seats.Length != BattleConst.SeatCount|| seatIndex >= BattleConst.SeatCount)
        {
            throw new ArgumentException("签发战斗 Token 时必须提供四个席位和有效的当前席位。");
        }

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(BattleConst.TokenRandomBytes));
        tickets[token] = new BattleTicket
        {
            initData = initData,
            seatIndex = seatIndex,
            expireTime = DateTime.UtcNow.AddMinutes(1),
        };
        return token;
    }

    /// <summary>
    /// 取得已经认证并完成解码的玩家输入。
    /// </summary>
    public bool TryGetInput(out BattleClientInput input)
    {
        return inputQueue.TryDequeue(out input);
    }

    /// <summary>
    /// 停止 KCP 网络线程和端口监听，并清理连接、入场券和未处理操作。
    /// </summary>
    public void Close()
    {
        isRunning = false;
        thread?.Join();
        thread = null;

        server?.Stop();
        server = null;
        sessions.Clear();
        tickets.Clear();

        while (inputQueue.TryDequeue(out _))
        {
        }
    }

    /// <summary>
    /// 持续推动 KCP 处理收到的数据、待发送数据、确认和重传。
    /// </summary>
    private void Run()
    {
        while (isRunning)
        {
            server!.TickIncoming();
            server.TickOutgoing();
            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// KCP 建立连接时触发；此时尚未通过战斗 Token 认证。
    /// </summary>
    private void OnConnected(int connectionID)
    {
        Console.WriteLine("KCP 客户端已连接：" + connectionID);
    }

    /// <summary>
    /// KCP 消息统一入口。读取消息类型后，交给认证或玩家输入流程处理。
    /// </summary>
    private void OnData(int connectionID, ArraySegment<byte> bytes, KcpChannel channel)
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
                case BattlePacketType.Auth:
                    ReceiveAuth(
                        connectionID, BattlePacketCodec.Deserialize<BattleAuthPacket>(bytes));
                    break;

                case BattlePacketType.PlayerInput:
                    ReceiveInput(
                        connectionID, BattlePacketCodec.Deserialize<PlayerInputPacket>(bytes));
                    break;

                default:
                    Console.WriteLine(
                        $"KCP 连接 {connectionID} 上传了未处理的战斗消息：{type}");
                    break;
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"KCP 连接 {connectionID} 的战斗消息解析失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 给客户端上传的操作补上服务端确认的战斗和席位身份，再放入输入队列。
    /// </summary>
    private void ReceiveInput(int connectionID, PlayerInputPacket packet)
    {
        if (!sessions.TryGetValue(connectionID, out BattleSession session))
        {
            Console.WriteLine($"未认证的 KCP 连接 {connectionID} 尝试上传玩家操作。");
            return;
        }

        if (packet.targetFrame < 0)
        {
            Console.WriteLine($"KCP 连接 {connectionID} 上传了无效目标帧：{packet.targetFrame}");
            return;
        }

        inputQueue.Enqueue(new BattleClientInput(session.initData.battleID, session.seatIndex, packet));
    }

    /// <summary>
    /// 使用一次性 Token 认证 KCP 连接；成功后建立 Session，并下发席位和战斗初始数据。
    /// </summary>
    private void ReceiveAuth(int connectionID, BattleAuthPacket packet)
    {
        if (!tickets.TryRemove(packet.battleToken, out BattleTicket? ticket)
            || ticket.expireTime <= DateTime.UtcNow)
        {
            Send(connectionID, BattlePacketCodec.Serialize(new BattleAuthResultPacket
            {
                result = BattleAuthResult.Failed,
            }));
            Console.WriteLine("KCP 战斗认证失败：" + connectionID);
            return;
        }

        sessions[connectionID] = new BattleSession
        {
            initData = ticket.initData,
            seatIndex = ticket.seatIndex,
        };

        Send(connectionID, BattlePacketCodec.Serialize(new BattleAuthResultPacket
        {
            result = BattleAuthResult.Success,
            seatIndex = ticket.seatIndex,
        }));
        Send(connectionID, BattlePacketCodec.Serialize(ticket.initData));

        Console.WriteLine( $"KCP 战斗认证成功：连接 {connectionID}，战斗 {ticket.initData.battleID}，席位 {ticket.seatIndex}");
    }

    /// <summary>
    /// 客户端断开后删除该连接保存的战斗身份。
    /// </summary>
    private void OnDisconnected(int connectionID)
    {
        sessions.Remove(connectionID);
        Console.WriteLine("KCP 客户端已断开：" + connectionID);
    }

    /// <summary>
    /// 记录 kcp2k 报告的连接错误。
    /// </summary>
    private static void OnError(int connectionID, ErrorCode error, string message)
    {
        Console.WriteLine($"KCP 连接 {connectionID} 错误：{error}，{message}");
    }

    /// <summary>
    /// 通过 KCP Reliable 通道向指定连接发送一条完整战斗消息。
    /// </summary>
    private void Send(int connectionID, byte[] bytes)
    {
        server!.Send(connectionID, new ArraySegment<byte>(bytes), KcpChannel.Reliable);
    }

    /// <summary>
    /// 创建客户端和服务端共同使用的 KCP 传输参数。
    /// </summary>
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
