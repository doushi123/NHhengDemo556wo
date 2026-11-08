using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using kcp2k;

/// <summary>
/// 独立 C# 服务端中的 KCP 战斗通道。
/// 负责 Token 认证、按逻辑帧汇总输入和广播权威操作帧。
/// 战斗期间只推进帧号，不运行角色、碰撞或技能模拟。
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

    /// <summary>一局战斗的中继运行数据，保存初始数据、广播帧号、连接和待汇总输入。</summary>
    private sealed class BattleContext
    {
        public BattleInitData initData;
        /// <summary>最后已确定并广播的操作帧；开局前为 -1，首帧为 0。</summary>
        public int frame = -1;
        /// <summary>
        /// 四个席位对应的KCP连接ID
        /// </summary>
        public int[] connections = new int[BattleConst.SeatCount];
        /// <summary>
        /// 每个席位上一帧采用的操作
        /// </summary>
        public BattleInput[] lastInputs = new BattleInput[BattleConst.SeatCount];
        /// <summary>
        /// 尚未广播的各帧输入
        /// </summary>
        public Dictionary<int, BattleInput[]> pending = new();
        /// <summary>
        /// 哪些席位确实上传了输入
        /// </summary>
        public Dictionary<int, bool[]> received = new();
        /// <summary>
        /// 是否开始广播操作帧
        /// </summary>
        public bool started;
        /// <summary>四个席位是否已完成场景、模拟和表现初始化；认证成功不代表就绪。</summary>
        public bool[] ready = new bool[BattleConst.SeatCount];
        public DateTime loadDeadline = DateTime.UtcNow.AddSeconds(BattleConst.LoadTimeoutSeconds);
        public volatile bool cancelRequested;
        /// <summary>是否由自动测试入口创建的战斗，不表示 TCP 等待房间或全局测试开关。</summary>
        public bool isTestBattle;
        public long nextFrameAt;
    }

    
    /// <summary>
    /// 保存尚未使用的临时战斗凭证。
    /// 房间系统创建 Token 后写入，KCP 客户端认证成功时取出并删除。
    /// </summary>
    private ConcurrentDictionary<string, BattleTicket> tickets = new();

    /// <summary>
    /// 保存已通过认证的 KCP 连接及其战斗身份。
    /// 收到玩家操作时，根据 connectionID 确认它属于哪场战斗和哪个席位。
    /// </summary>
    private Dictionary<int, BattleSession> sessions = new();

    /// <summary>按战斗 ID 保存各局的中继运行数据，供认证、输入汇总和帧广播查找。</summary>
    private ConcurrentDictionary<long, BattleContext> battles = new();

    // TCP 线程只提交取消请求，实际断开 KCP 和清理会话由 KCP 线程执行。
    private ConcurrentQueue<(long battleID, string error)> cancellations = new();
    private long nextBattleID = DateTime.UtcNow.Ticks;

    /// <summary>通知房间模块：所有真人已就绪，本局开始开帧。由 KCP 线程触发。</summary>
    public event Action<long>? BattleStarted;

    /// <summary>通知房间模块：本次入场或战斗已经取消。由 KCP 线程触发。</summary>
    public event Action<long, string>? BattleCancelled;

    /// <summary>正式和测试战斗共用同一个递增 ID 来源，避免两条链路碰撞。</summary>
    public long NextBattleID()
    {
        return Interlocked.Increment(ref nextBattleID);
    }

    /// <summary>请求停止一局的开帧与入场，排队交给 KCP 线程清理。</summary>
    public void CancelBattle(long battleID, string error)
    {
        if (battles.TryGetValue(battleID, out BattleContext? battle))
        {
            battle.cancelRequested = true;
            cancellations.Enqueue((battleID, error));
        }
    }

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
    /// <param name="isTestBattle">本局是否由自动测试入口创建。</param>
    /// <param name="uploadedMapJson">仅测试战斗使用的客户端上传文本，来自正式导出地图；正式链路不传，读取服务端地图文件。</param>
    public string CreateToken(BattleInitData initData, int seatIndex,
        bool isTestBattle = false, string? uploadedMapJson = null)
    {
        if (initData.seats == null || initData.seats.Length != BattleConst.SeatCount ||
            seatIndex < 0 || seatIndex >= BattleConst.SeatCount)
        {
            throw new ArgumentException("签发战斗 Token 时必须提供四个席位和有效的当前席位。");
        }

        BattleContext battle = battles.GetOrAdd(initData.battleID,
            _ => CreateBattle(initData, isTestBattle, uploadedMapJson));

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(BattleConst.TokenRandomBytes));
        tickets[token] = new BattleTicket
        {
            initData = battle.initData,
            seatIndex = seatIndex,
            expireTime = battle.loadDeadline,
        };
        return token;
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
        battles.Clear();
        while (cancellations.TryDequeue(out _)) { }
    }

    /// <summary>
    /// 持续推动 KCP 处理收到的数据、待发送数据、确认和重传。
    /// </summary>
    private void Run()
    {
        while (isRunning)
        {
            while (cancellations.TryDequeue(out var cancellation))
            {
                RemoveBattle(cancellation.battleID, cancellation.error);
            }
            server!.TickIncoming();
            foreach (BattleContext battle in battles.Values)
            {
                if (battle.cancelRequested)
                {
                    continue;
                }
                if (!battle.started && DateTime.UtcNow >= battle.loadDeadline)
                {
                    CancelBattle(battle.initData.battleID, "等待玩家加载超时，本次开战已取消。");
                    continue;
                }
                if (battle.started && Stopwatch.GetTimestamp() >= battle.nextFrameAt)
                {
                    AdvanceFrame(battle);
                    battle.nextFrameAt += Stopwatch.Frequency / BattleConst.LogicFrameRate;
                }
            }
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

                case BattlePacketType.Ready:
                    ReceiveReady(connectionID);
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
    /// 用已认证的连接身份把输入放进对应战斗和目标帧。
    /// </summary>
    private void ReceiveInput(int connectionID, PlayerInputPacket packet)
    {
        if (!sessions.TryGetValue(connectionID, out BattleSession session))
        {
            Console.WriteLine($"未认证的 KCP 连接 {connectionID} 尝试上传玩家操作。");
            return;
        }

        if (!battles.TryGetValue(session.initData.battleID, out BattleContext? battle) ||
            !battle.started || battle.cancelRequested)
        {
            return;
        }
        if (packet.targetFrame <= battle.frame ||
            packet.targetFrame > battle.frame + BattleConst.LogicFrameRate)
        {
            Console.WriteLine($"KCP 连接 {connectionID} 上传了过期或过远的目标帧：{packet.targetFrame}");
            return;
        }
        if (!battle.pending.TryGetValue(packet.targetFrame, out BattleInput[]? inputs))
        {
            inputs = new BattleInput[BattleConst.SeatCount];
            battle.pending.Add(packet.targetFrame, inputs);
            battle.received.Add(packet.targetFrame, new bool[BattleConst.SeatCount]);
        }
        inputs[session.seatIndex] = packet.input;
        battle.received[packet.targetFrame][session.seatIndex] = true;
    }

    /// <summary>
    /// 使用一次性 Token 认证 KCP 连接；成功后建立 Session，并下发席位和战斗初始数据。
    /// </summary>
    private void ReceiveAuth(int connectionID, BattleAuthPacket packet)
    {
        if (!tickets.TryRemove(packet.battleToken, out BattleTicket? ticket)
            || ticket.expireTime <= DateTime.UtcNow
            || !battles.TryGetValue(ticket.initData.battleID, out BattleContext? battle)
            || battle.cancelRequested || battle.started)
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
        battle.connections[ticket.seatIndex] = connectionID;

        Send(connectionID, BattlePacketCodec.Serialize(new BattleAuthResultPacket
        {
            result = BattleAuthResult.Success,
            seatIndex = ticket.seatIndex,
        }));
        Send(connectionID, BattlePacketCodec.Serialize(ticket.initData));

        Console.WriteLine( $"KCP 战斗认证成功：连接 {connectionID}，战斗 {ticket.initData.battleID}，席位 {ticket.seatIndex}");
    }

    /// <summary>记录客户端就绪；全部真人都上报后，先发开始通知，再从第 0 帧广播。</summary>
    private void ReceiveReady(int connectionID)
    {
        if (!sessions.TryGetValue(connectionID, out BattleSession? session) ||
            !battles.TryGetValue(session.initData.battleID, out BattleContext? battle) ||
            battle.started || battle.cancelRequested)
        {
            return;
        }
        if (DateTime.UtcNow >= battle.loadDeadline)
        {
            CancelBattle(battle.initData.battleID, "等待玩家加载超时，本次开战已取消。");
            return;
        }
        battle.ready[session.seatIndex] = true;
        for (int seat = 0; seat < BattleConst.SeatCount; seat++)
        {
            if (battle.initData.seats[seat].playerID > 0 &&
                (battle.connections[seat] == 0 || !battle.ready[seat]))
            {
                return;
            }
        }
        battle.started = true;
        BattleStarted?.Invoke(battle.initData.battleID);
        byte[] bytes = BattlePacketCodec.Serialize(new BattleStartPacket { battleID = battle.initData.battleID });
        foreach (int id in battle.connections)
        {
            if (id != 0)
            {
                Send(id, bytes);
            }
        }
        battle.nextFrameAt = Stopwatch.GetTimestamp() + Stopwatch.Frequency / BattleConst.LogicFrameRate;
    }

    /// <summary>删除本局 Token 与会话，停止开帧并通知房间，避免失败后留下等待中的战斗。</summary>
    private void RemoveBattle(long battleID, string error)
    {
        if (!battles.TryRemove(battleID, out BattleContext? battle))
        {
            return;
        }
        foreach (var ticket in tickets)
        {
            if (ticket.Value.initData.battleID == battleID)
            {
                tickets.TryRemove(ticket.Key, out _);
            }
        }
        foreach (int id in battle.connections)
        {
            if (id != 0)
            {
                sessions.Remove(id);
                server!.Disconnect(id);
            }
        }
        BattleCancelled?.Invoke(battleID, error);
    }

    /// <summary>
    /// 客户端断开后删除该连接保存的战斗身份。
    /// </summary>
    private void OnDisconnected(int connectionID)
    {
        if (sessions.TryGetValue(connectionID, out BattleSession? session) &&
            battles.TryGetValue(session.initData.battleID, out BattleContext? battle))
        {
            battle.connections[session.seatIndex] = 0;
            battle.ready[session.seatIndex] = false;
            if (battle.isTestBattle || !battle.started || battle.connections.All(id => id == 0))
            {
                CancelBattle(session.initData.battleID, "玩家战斗连接已断开，本次入场已取消。");
            }
        }
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

    /// <summary>计算配置和地图 Hash，使用调用方提供的初始数据创建一局战斗的中继数据。</summary>
    /// <param name="uploadedMapJson">仅测试链路提供的正式地图上传文本；正式战斗始终读取部署目录的地图文件。</param>
    private static BattleContext CreateBattle(BattleInitData init, bool isTestBattle,
        string? uploadedMapJson)
    {
        string configJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, BattleConst.ConfigFileName));
        string mapJson = isTestBattle && uploadedMapJson != null
            ? uploadedMapJson
            : File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Maps", "Map_" + init.mapID + ".json"));
        init.configHash = BattleConfigSerializer.GetHash(configJson);
        init.mapHash = BattleConfigSerializer.GetHash(mapJson);
        return new BattleContext
        {
            initData = init,
            isTestBattle = isTestBattle,
        };
    }

    /// <summary>按顺序汇总并广播给所有客户端下一帧输入；缺输入时沿用移动方向并清掉一次性按钮。</summary>
    private void AdvanceFrame(BattleContext battle)
    {
        int frame = battle.frame + 1;
        BattleInput[] inputs = new BattleInput[BattleConst.SeatCount];
        //找到之前收到的这一帧输入
        battle.pending.TryGetValue(frame, out BattleInput[]? pending);
        battle.received.TryGetValue(frame, out bool[]? received);
        for (int seat = 0; seat < BattleConst.SeatCount; seat++)
        {
            if (battle.initData.seats[seat].playerID == 0 || battle.connections[seat] == 0)
            {
                battle.lastInputs[seat] = default;
                continue;
            }
            inputs[seat] = received != null && received[seat] ? pending![seat] : battle.lastInputs[seat];
            if (received == null || !received[seat])
            {
                inputs[seat].buttons = BattleButtons.None;
            }
            battle.lastInputs[seat] = inputs[seat];
        }
        //删除已经处理完的暂存数据
        battle.pending.Remove(frame);
        battle.received.Remove(frame);

        AuthorityFramePacket packet = new AuthorityFramePacket { frame = frame, inputs = inputs };
        battle.frame = frame;
        byte[] bytes = BattlePacketCodec.Serialize(packet);
        foreach (int connectionID in battle.connections)
        {
            if (connectionID != 0)
            {
                Send(connectionID, bytes);
            }
        }
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
