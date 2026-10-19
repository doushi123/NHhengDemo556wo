using System.Net;
using System.Net.Sockets;

public sealed class ServerSocket
{
    private readonly object clientLock = new object();
    private readonly List<ClientSocket> clients = new List<ClientSocket>();
    private readonly AccountDataStore dataStore;
    private readonly WorldChatStore worldChatStore;

    private Socket? socket;
    private volatile bool isRunning;
    private long nextWorldChatMessageID;

    public ServerSocket(string dataDirectory)
    {
        dataStore = new AccountDataStore(dataDirectory);
        worldChatStore = new WorldChatStore(dataDirectory);
    }

    public void Start(int port)
    {
        // 启动时扫描并缓存本程序集中的所有 NetMessage。
        NetSerializer.RegisterMessages(typeof(ServerSocket).Assembly);
        dataStore.Load();
        worldChatStore.Load();

        // 接着历史里最大的消息编号继续发号，否则重启后新消息会和历史消息 ID 重复。
        nextWorldChatMessageID = worldChatStore.GetLatestMessageID();

        Console.WriteLine("已载入世界聊天历史：" + worldChatStore.Count + " 条");

        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Any, port));
        socket.Listen(10);
        isRunning = true;

        ThreadPool.QueueUserWorkItem(AcceptClients);
        Console.WriteLine("正在监听 TCP 端口：" + port);
    }

    public bool TryGetPlayerData(int playerID, out PlayerProfileData? playerData)
    {
        return dataStore.TryGetPlayerData(playerID, out playerData);
    }

    public bool TryGetFriendsData(int playerID, out PlayerSocialData? data)
    {
        return dataStore.TryGetFriendsData(playerID, out data);
    }

    public bool TrySearchPlayer(string account, out PlayerProfileData? playerData, out string error)
    {
        return dataStore.TrySearchPlayer(account, out playerData, out error);
    }

    public bool TrySendFriendRequest(
        int senderPlayerID,
        int targetPlayerID,
        string message,
        out PlayerSocialData? targetSocialData,
        out string error)
    {
        return dataStore.TrySendFriendRequest(
            senderPlayerID, targetPlayerID, message, out targetSocialData, out error);
    }

    public bool TryHandleFriendRequest(
        int receiverPlayerID,
        int senderPlayerID,
        bool accept,
        out PlayerSocialData? receiverSocialData,
        out PlayerSocialData? senderSocialData,
        out string error)
    {
        return dataStore.TryHandleFriendRequest(
            receiverPlayerID, senderPlayerID, accept, out receiverSocialData,
            out senderSocialData, out error);
    }

    /// <summary>
    /// 玩家在线时立即推送最新好友数据；离线时数据已经保存在 JSON，登录后会正常下发。
    /// </summary>
    public void SendFriendsDataToPlayer(int playerID, PlayerSocialData data)
    {
        ClientSocket? targetClient;
        lock (clientLock)
        {
            targetClient = clients.FirstOrDefault(client => client.PlayerID == playerID);
        }

        targetClient?.Send(new FriendsDataMsg
        {
            data = data,
        });
    }

    public bool TryLogin(
        string account,
        string password,
        out PlayerProfileData? playerData,
        out PlayerSocialData? socialData,
        out string error)
    {
        return dataStore.TryLogin(account, password, out playerData, out socialData, out error);
    }

    public bool TryRegister(
        string account,
        string password,
        out PlayerProfileData? playerData,
        out PlayerSocialData? socialData,
        out string error)
    {
        return dataStore.TryRegister(account, password, out playerData, out socialData, out error);
    }

    public WorldChatData CreateWorldChatData(PlayerProfileData player, string content)
    {
        return new WorldChatData
        {
            messageID = Interlocked.Increment(ref nextWorldChatMessageID),
            senderPlayerID = player.playerID,
            senderAccount = player.account,
            content = content,
            sendTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
    }

    /// <summary>
    /// 把一条已经确认的消息记入世界频道历史，供之后登录的玩家接收。
    /// 只有真正广播出去的消息才应该调用，发送失败的消息不进历史。
    /// </summary>
    public void RecordWorldChat(WorldChatData data)
    {
        worldChatStore.Add(data);
    }

    public WorldChatData[] GetWorldChatHistory()
    {
        return worldChatStore.GetHistory();
    }

    public void BroadcastWorldChat(WorldChatMsg msg)
    {
        ClientSocket[] onlineClients;
        lock (clientLock)
        {
            onlineClients = clients.Where(client => client.IsLoggedIn).ToArray();
        }

        foreach (ClientSocket client in onlineClients)
        {
            client.Send(msg);
        }
    }

    public void RemoveClient(ClientSocket client)
    {
        lock (clientLock)
        {
            clients.Remove(client);
        }

        client.Close();
    }

    public void Close()
    {
        isRunning = false;
        socket?.Close();
        socket = null;

        lock (clientLock)
        {
            foreach (ClientSocket client in clients)
            {
                client.Close();
            }

            clients.Clear();
        }

        // 所有连接停止后，等待世界聊天保存队列中的任务全部写完再退出服务端。
        worldChatStore.Close();
    }

    private void AcceptClients(object? state)
    {
        while (isRunning)
        {
            try
            {
                Socket clientSocket = socket!.Accept();
                ClientSocket client = new ClientSocket(this, clientSocket);

                lock (clientLock)
                {
                    clients.Add(client);
                }

                Console.WriteLine("客户端已连接：" + clientSocket.RemoteEndPoint);
                client.Start();
            }
            catch (SocketException) when (!isRunning)
            {
                return;
            }
        }
    }
}
