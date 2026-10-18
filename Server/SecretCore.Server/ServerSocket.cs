using System.Net;
using System.Net.Sockets;

public sealed class ServerSocket
{
    private readonly object clientLock = new object();
    private readonly List<ClientSocket> clients = new List<ClientSocket>();
    private readonly AccountDataStore dataStore;

    private Socket? socket;
    private volatile bool isRunning;

    public ServerSocket(string dataDirectory)
    {
        dataStore = new AccountDataStore(dataDirectory);
    }

    public void Start(int port)
    {
        // 启动时扫描并缓存本程序集中的所有 NetMessage。
        NetSerializer.RegisterMessages(typeof(ServerSocket).Assembly);
        dataStore.Load();

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
