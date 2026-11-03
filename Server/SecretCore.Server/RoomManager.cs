using System.Net.Sockets;

/// <summary>
/// 维护 TCP 房间的成员、席位与房主，向成员推送完整房间数据。
/// </summary>
public sealed class RoomManager
{
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
                    SendError(client, operation, "正式开战流程尚未接入。");
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
