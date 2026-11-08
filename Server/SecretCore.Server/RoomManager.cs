using System.Net.Sockets;

/// <summary>
/// 维护 TCP 房间的成员、席位与房主，向成员推送完整房间数据。
/// </summary>
public sealed class RoomManager
{
    private BattleKcpServer battleServer;

    /// <summary>房间负责发起正式入场，并接收 KCP 的统一开始与取消通知。</summary>
    public RoomManager(BattleKcpServer battleServer)
    {
        this.battleServer = battleServer;
        battleServer.BattleStarted += OnBattleStarted;
        battleServer.BattleCancelled += OnBattleCancelled;
    }

    /// <summary>
    /// 房间数据的锁，防止多个线程同时修改房间数据
    /// </summary>
    private object roomLock = new object();

    /// <summary>
    /// 房间号对应的房间，用于按号加入和空房销毁。
    /// </summary>
    private Dictionary<int, Room> rooms = new Dictionary<int, Room>();

    /// <summary>
    /// 根据玩家连接查找所在房间
    /// </summary>
    private Dictionary<ClientSocket, Room> memberships = new Dictionary<ClientSocket, Room>();

    /// <summary>
    /// 待发送消息的队列
    /// </summary>
    private Queue<(ClientSocket client, BaseMsg msg)> notifications =new Queue<(ClientSocket client, BaseMsg msg)>();

    private int nextRoomID;
    private bool isSending;

    /// <summary>服务端管理一个房间所需的数据。</summary>
    private class Room
    {
        // 房间号、房主、成员等信息，发给客户端显示。
        public RoomData data = new RoomData();

        // 每个席位的玩家连接，用来给房间成员发消息；null 表示空位。
        public ClientSocket?[] clients = new ClientSocket?[BattleConst.SeatCount];
        /// <summary>本房间正在加载或进行的战斗，等待阶段为 0。</summary>
        public long battleID;
    }

    /// <summary>处理已由 ClientSocket 分类的房间请求，身份只取自该连接的登录资料。</summary>
    public void Handle(ClientSocket client, PlayerProfileData? player, BaseMsg msg)
    {
        OperationType operation = msg switch
        {
            CreateRoomRequestMsg => OperationType.CreateRoom,
            JoinRoomRequestMsg => OperationType.JoinRoom,
            LeaveRoomRequestMsg => OperationType.LeaveRoom,
            StartBattleRequestMsg => OperationType.StartBattle,
            ChangeSeatRequestMsg => OperationType.ChangeSeat,
            BattleLoadFailedMsg => OperationType.StartBattle,
            _ => throw new ArgumentException("不是房间请求。", nameof(msg)),
        };

        // 多个连接在不同接收线程执行，成员变化与通知入队必须按同一个顺序完成。
        lock (roomLock)
        {
            if (player == null)
            {
                SendError(client, operation, "登录后才能操作房间。");
                return;
            }

            switch (msg)
            {
                case CreateRoomRequestMsg request:
                    Create(client, player, request.mode);
                    break;
                case JoinRoomRequestMsg request:
                    Join(client, player, request.roomID);
                    break;
                case LeaveRoomRequestMsg:
                    Leave(client);
                    break;
                case StartBattleRequestMsg:
                    StartBattle(client, player);
                    break;
                case ChangeSeatRequestMsg request:
                    ChangeSeat(client, request.seatIndex);
                    break;
                case BattleLoadFailedMsg request:
                    if (memberships.TryGetValue(client, out Room? room) &&
                        room.data.state == RoomState.Loading && room.battleID == request.battleID)
                    {
                        battleServer.CancelBattle(request.battleID, player.account + " 加载失败：" + request.error);
                    }
                    break;
            }
        }
    }

    /// <summary>断线时仅移除这条连接的席位。</summary>
    public void Disconnect(ClientSocket client)
    {
        lock (roomLock)
        {
            if (memberships.TryGetValue(client, out Room? room))
            {
                if (room.battleID != 0)
                {
                    battleServer.CancelBattle(room.battleID, "房间成员已断线，本次战斗已取消。");
                }
                RemoveMember(client, room);
            }
        }
    }

    /// <summary>服务端关闭时清空内存房间与待发送通知。</summary>
    public void Clear()
    {
        lock (roomLock)
        {
            rooms.Clear();
            memberships.Clear();
            notifications.Clear();
        }
    }

    /// <summary>创建房间，将创建者设为房主并加入席位 0，然后下发房间信息。</summary>
    private void Create(ClientSocket client, PlayerProfileData player, BattleMode mode)
    {
        int mapID;
        switch (mode)
        {
            case BattleMode.CoreWar:
                mapID = 1;
                break;
            default:
                SendError(client, OperationType.CreateRoom, "不支持该玩法模式。");
                return;
        }

        // 只确认正式地图文件是否存在。
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Maps", $"Map_{mapID}.json")))
        {
            SendError(client, OperationType.CreateRoom, $"服务端缺少正式地图 Map_{mapID}.json，请先导出并重新构建服务端。");
            return;
        }

        Room room = new Room();
        room.data.roomID = ++nextRoomID;
        room.data.ownerPlayerID = player.playerID;
        room.data.mapID = mapID;
        room.data.mode = mode;
        room.data.state = RoomState.Waiting;
        AddMember(client, player, room, 0);
        rooms.Add(room.data.roomID, room);
        Broadcast(room);
    }

    /// <summary>
    /// 让玩家通过房间号加入房间，分配空席位后向所有成员发送最新房间信息。
    /// 房间不存在、已满或已经开始时，向该玩家返回失败原因。
    /// </summary>
    private void Join(ClientSocket client, PlayerProfileData player, int roomID)
    {
        if (!rooms.TryGetValue(roomID, out Room? room))
        {
            SendError(client, OperationType.JoinRoom, "房间不存在或已解散。");
            return;
        }
        if (room.data.state != RoomState.Waiting)
        {
            SendError(client, OperationType.JoinRoom, "房间已开始加载或战斗，无法加入。");
            return;
        }

        int seat = Array.IndexOf(room.clients, null);
        if (seat < 0)
        {
            SendError(client, OperationType.JoinRoom, "房间已满。");
            return;
        }

        AddMember(client, player, room, seat);
        Broadcast(room);
    }

    /// <summary>将玩家移动到指定空席位，释放原席位后向所有成员下发最新房间数据。</summary>
    private void ChangeSeat(ClientSocket client, int seatIndex)
    {
        if (!memberships.TryGetValue(client, out Room? room))
        {
            SendError(client, OperationType.ChangeSeat, "当前不在房间中。");
            return;
        }
        if (room.data.state != RoomState.Waiting)
        {
            SendError(client, OperationType.ChangeSeat, "房间已进入开战流程，不能切换席位。");
            return;
        }
        if (seatIndex < 0 || seatIndex >= room.clients.Length)
        {
            SendError(client, OperationType.ChangeSeat, "席位编号无效。");
            return;
        }
        if (room.clients[seatIndex] != null)
        {
            SendError(client, OperationType.ChangeSeat, "该席位已有人，请选择其他空位。");
            return;
        }

        int oldSeat = Array.IndexOf(room.clients, client);
        room.clients[seatIndex] = client;
        room.data.seats[seatIndex] = room.data.seats[oldSeat];
        room.clients[oldSeat] = null;
        room.data.seats[oldSeat] = null!;
        // 只改变席位，房主身份和玩家所属房间保持不变。
        Broadcast(room);
    }

    /// <summary>检查房主和房间阶段，读取四个手动出生点，为真人签发 Token 并通知加载。</summary>
    private void StartBattle(ClientSocket client, PlayerProfileData player)
    {
        if (!memberships.TryGetValue(client, out Room? room))
        {
            SendError(client, OperationType.StartBattle, "当前不在房间中。");
            return;
        }
        if (room.data.ownerPlayerID != player.playerID)
        {
            SendError(client, OperationType.StartBattle, "只有房主可以开始游戏。");
            return;
        }
        if (room.data.state != RoomState.Waiting)
        {
            SendError(client, OperationType.StartBattle, "房间已开始加载或战斗。");
            return;
        }

        long battleID = battleServer.NextBattleID();
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Maps", $"Map_{room.data.mapID}.json");
            MapData map = MapSerializer.Deserialize(File.ReadAllText(path));
            if (map.mapID != room.data.mapID || map.spawns == null || map.spawns.Length != BattleConst.SeatCount)
            {
                throw new InvalidDataException("地图缺少四个手动出生点，请重新导出地图并构建服务端。");
            }
            BattleInitData init = new BattleInitData
            {
                battleID = battleID,
                mapID = room.data.mapID,
                mode = room.data.mode,
                randomSeed = Random.Shared.Next(),
                seats = new BattleSeatData[BattleConst.SeatCount],
            };
            for (int seat = 0; seat < init.seats.Length; seat++)
            {
                MapVector spawn = map.spawns[seat];
                init.seats[seat] = new BattleSeatData
                {
                    playerID = room.data.seats[seat] == null ? 0 : room.data.seats[seat].playerID,
                    camp = seat < 2 ? BattleCamp.CampA : BattleCamp.CampB,
                    spawnX = (float)spawn.x,
                    spawnY = (float)spawn.y,
                    spawnZ = (float)spawn.z,
                };
            }

            // 全部 Token 成功签发后再通知成员，避免一部分人入场、另一部分仍在房间。
            string[] tokens = new string[BattleConst.SeatCount];
            for (int seat = 0; seat < tokens.Length; seat++)
            {
                if (room.clients[seat] != null)
                {
                    tokens[seat] = battleServer.CreateToken(init, seat);
                }
            }
            room.battleID = battleID;
            room.data.state = RoomState.Loading;
            Broadcast(room);
            for (int seat = 0; seat < tokens.Length; seat++)
            {
                if (room.clients[seat] != null)
                {
                    Enqueue(room.clients[seat]!, new BattleEntryMsg
                    {
                        battleID = battleID,
                        mapID = init.mapID,
                        battleToken = tokens[seat],
                    });
                }
            }
        }
        catch (Exception exception) when (exception is IOException || exception is InvalidOperationException ||
            exception is System.Text.Json.JsonException || exception is UnauthorizedAccessException)
        {
            battleServer.CancelBattle(battleID, exception.Message);
            SendError(client, OperationType.StartBattle, "无法开始游戏：" + exception.Message);
        }
    }

    /// <summary>全部真人已就绪时，把对应房间改为战斗中并通知成员。</summary>
    private void OnBattleStarted(long battleID)
    {
        lock (roomLock)
        {
            Room? room = rooms.Values.FirstOrDefault(item => item.battleID == battleID);
            if (room != null)
            {
                room.data.state = RoomState.InBattle;
                Broadcast(room);
            }
        }
    }

    /// <summary>取消本局后恢复等待状态，先刷新房间，再通知成员显示取消原因。</summary>
    private void OnBattleCancelled(long battleID, string error)
    {
        lock (roomLock)
        {
            Room? room = rooms.Values.FirstOrDefault(item => item.battleID == battleID);
            if (room == null)
            {
                return;
            }
            room.battleID = 0;
            room.data.state = RoomState.Waiting;
            // 先打开房间，再显示取消提示，避免新开的房间面板盖住提示。
            Broadcast(room);
            foreach (ClientSocket? member in room.clients)
            {
                if (member != null)
                {
                    Enqueue(member, new BattleCancelledMsg { battleID = battleID, error = error });
                }
            }
        }
    }

    /// <summary>
    /// 将玩家加入房间的指定席位，记录其连接、玩家资料和所在房间。
    /// </summary>
    private void AddMember(ClientSocket client, PlayerProfileData player, Room room, int seat)
    {
        memberships.Add(client, room);
        room.clients[seat] = client;
        room.data.seats[seat] = new PlayerProfileData
        {
            playerID = player.playerID,
            account = player.account,
            level = player.level,
            state = PlayerOnlineState.InTeam,
        };
    }

    /// <summary>
    /// 处理玩家主动退出房间的请求，移除玩家并向其发送退出确认。
    /// 其余成员会收到更新后的房间信息。
    /// </summary>
    private void Leave(ClientSocket client)
    {
        if (!memberships.TryGetValue(client, out Room? room))
        {
            SendError(client, OperationType.LeaveRoom, "当前不在房间中。");
            return;
        }
        if (room.data.state != RoomState.Waiting)
        {
            SendError(client, OperationType.LeaveRoom, "房间已进入开战流程，不能按等待房间退出。");
            return;
        }

        RemoveMember(client, room);
        Enqueue(client, new RoomLeftMsg());
    }

    /// <summary>
    /// 从房间中移除玩家，清空其席位并删除该玩家的入房记录。
    /// 房主离开时，将房主转交给席位最靠前的剩余成员；没有成员时删除房间。
    /// 房间仍有人时，向剩余成员发送最新房间信息。
    /// </summary>
    private void RemoveMember(ClientSocket client, Room room)
    {
        int seat = Array.IndexOf(room.clients, client);
        int playerID = room.data.seats[seat].playerID;
        room.clients[seat] = null;
        room.data.seats[seat] = null!;
        memberships.Remove(client);

        int firstSeat = Array.FindIndex(room.clients, member => member != null);
        if (firstSeat < 0)
        {
            rooms.Remove(room.data.roomID);
            return;
        }
        if (room.data.ownerPlayerID == playerID)
        {
            room.data.ownerPlayerID = room.data.seats[firstSeat].playerID;
        }
        Broadcast(room);
    }

    /// <summary>
    /// 向房间所有成员发送当前房间信息。
    /// 先复制要发送的数据，再加入消息队列等待发送。
    /// </summary>
    private void Broadcast(Room room)
    {
        RoomDataMsg msg = new RoomDataMsg
        {
            data = new RoomData
            {
                roomID = room.data.roomID,
                ownerPlayerID = room.data.ownerPlayerID,
                mapID = room.data.mapID,
                mode = room.data.mode,
                state = room.data.state,
                // 复制席位数组，避免等待发送时，房间成员变化影响这条消息的内容。
                seats = (PlayerProfileData[])room.data.seats.Clone(),
            },
        };

        foreach (ClientSocket? member in room.clients)
        {
            if (member != null)
            {
                Enqueue(member, msg);
            }
        }
    }

    /// <summary>
    /// 向发起请求的客户端发送操作失败的原因，供客户端显示提示。
    /// </summary>
    private void SendError(ClientSocket client, OperationType operation, string error)
    {
        Enqueue(client, new OperationResultMsg { operation = operation, success = false, error = error });
    }

    /// <summary>
    /// 将要发给指定客户端的消息加入发送队列。
    /// 当前没有发送任务时，启动一个任务按顺序发送。
    /// </summary>
    /// <remarks>调用此方法时需要持有 roomLock。</remarks>
    private void Enqueue(ClientSocket client, BaseMsg msg)
    {
        notifications.Enqueue((client, msg));
        if (!isSending)
        {
            isSending = true;
            ThreadPool.QueueUserWorkItem(SendNotifications);
        }
    }

    /// <summary>
    /// 按入队顺序发送队列中的消息，全部发完后结束任务。
    /// 某个客户端发送失败时，关闭该连接并继续发送后面的消息。
    /// </summary>
    private void SendNotifications(object? state)
    {
        while (true)
        {
            (ClientSocket client, BaseMsg msg) notification;
            lock (roomLock)
            {
                if (notifications.Count == 0)
                {
                    isSending = false;
                    return;
                }
                notification = notifications.Dequeue();
            }

            try
            {
                notification.client.Send(notification.msg);
            }
            catch (Exception exception) when (exception is SocketException || exception is ObjectDisposedException)
            {
                Console.WriteLine("房间消息发送失败，关闭连接：" + exception.Message);
                notification.client.Close();
            }
        }
    }
}
