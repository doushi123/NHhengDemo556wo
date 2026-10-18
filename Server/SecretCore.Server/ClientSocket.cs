using System.Net.Sockets;

public sealed class ClientSocket
{
    private const int MaxMessageBodySize = 1024 * 1024;

    private readonly ServerSocket server;
    private readonly Socket socket;
    private readonly object sendLock = new object();
    private readonly byte[] receiveBytes = new byte[8192];
    private readonly byte[] cacheBytes = new byte[MaxMessageBodySize + 8];

    private int cacheNum;
    private volatile bool isConnected = true;
    private PlayerProfileData? currentPlayer;

    public bool IsLoggedIn => currentPlayer != null;

    public ClientSocket(ServerSocket server, Socket socket)
    {
        this.server = server;
        this.socket = socket;
    }

    public void Start()
    {
        ThreadPool.QueueUserWorkItem(ReceiveMessages);
    }

    public void Send(BaseMsg msg)
    {
        // 消息类只保存数据，统一由 NetSerializer 转成完整网络包。
        byte[] bytes = NetSerializer.Serialize(msg);

        lock (sendLock)
        {
            int sentNum = 0;
            while (sentNum < bytes.Length)
            {
                sentNum += socket.Send(bytes, sentNum, bytes.Length - sentNum, SocketFlags.None);
            }
        }
    }

    public void Close()
    {
        if (!isConnected)
        {
            return;
        }

        isConnected = false;

        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }

        socket.Close();
    }

    private void ReceiveMessages(object? state)
    {
        try
        {
            while (isConnected)
            {
                int receiveNum = socket.Receive(receiveBytes);
                if (receiveNum == 0)
                {
                    break;
                }

                HandleReceiveMessages(receiveBytes, receiveNum);
            }
        }
        catch (Exception exception)
        {
            if (isConnected)
            {
                Console.WriteLine("客户端通信出错：" + exception.Message);
            }
        }
        finally
        {
            server.RemoveClient(this);
            Console.WriteLine("客户端已断开。");
        }
    }

    private void HandleReceiveMessages(byte[] bytes, int length)
    {
        if (cacheNum + length > cacheBytes.Length)
        {
            throw new InvalidOperationException("接收缓存已满。");
        }

        Array.Copy(bytes, 0, cacheBytes, cacheNum, length);
        cacheNum += length;

        int index = 0;
        while (cacheNum - index >= 8)
        {
            int msgID = BitConverter.ToInt32(cacheBytes, index);
            int bodyLength = BitConverter.ToInt32(cacheBytes, index + 4);

            if (bodyLength < 0 || bodyLength > MaxMessageBodySize)
            {
                throw new InvalidOperationException("非法消息长度：" + bodyLength);
            }

            int messageLength = 8 + bodyLength;
            if (cacheNum - index < messageLength)
            {
                break;
            }

            // 公共消息头已经解析完，这里只把消息体恢复成具体消息对象。
            BaseMsg? msg = NetSerializer.Deserialize(msgID, cacheBytes, index + 8, bodyLength);
            if (msg != null)
            {
                HandleMessage(msg);
            }

            index += messageLength;
        }

        if (index > 0)
        {
            Array.Copy(cacheBytes, index, cacheBytes, 0, cacheNum - index);
            cacheNum -= index;
        }
    }

    private void HandleMessage(BaseMsg msg)
    {
        if (msg is LoginRequestMsg loginRequest)
        {
            HandleLogin(loginRequest);
            return;
        }

        if (msg is RegisterRequestMsg registerRequest)
        {
            HandleRegister(registerRequest);
            return;
        }

        if (msg is RequestPlayerDataMsg playerRequest)
        {
            SendPlayerData(playerRequest);
            return;
        }

        if (msg is RequestFriendsDataMsg friendsRequest)
        {
            SendFriendsData(friendsRequest);
            return;
        }

        if (msg is SendWorldChatMsg worldChatRequest)
        {
            HandleWorldChat(worldChatRequest);
        }
    }

    private void HandleLogin(LoginRequestMsg request)
    {
        bool success = server.TryLogin(
            request.account,
            request.password,
            out PlayerProfileData? playerData,
            out PlayerSocialData? socialData,
            out string error);

        if (success)
        {
            currentPlayer = playerData;
        }

        Send(new LoginResponseMsg
        {
            success = success,
            error = error,
            initialData = success ? CreateLoginInitialData(playerData, socialData) : null,
        });
    }

    private void HandleRegister(RegisterRequestMsg request)
    {
        bool success = server.TryRegister(
            request.account,
            request.password,
            out PlayerProfileData? playerData,
            out PlayerSocialData? socialData,
            out string error);

        if (success)
        {
            currentPlayer = playerData;
        }

        Send(new RegisterResponseMsg
        {
            success = success,
            error = error,
            initialData = success ? CreateLoginInitialData(playerData, socialData) : null,
        });
    }

    /// <summary>
    /// 统一组装登录或注册成功后进入大厅需要的首批数据。
    /// </summary>
    private LoginInitialData CreateLoginInitialData(
        PlayerProfileData? playerData,
        PlayerSocialData? socialData)
    {
        return new LoginInitialData
        {
            playerData = playerData,
            socialData = socialData,
            worldChatHistory = server.GetWorldChatHistory(),
        };
    }

    private void SendPlayerData(RequestPlayerDataMsg request)
    {
        if (server.TryGetPlayerData(request.playerID, out PlayerProfileData? playerData))
        {
            Send(new PlayerDataMsg { success = true, data = playerData });
            return;
        }

        Send(new PlayerDataMsg { success = false, error = "没有找到该玩家。" });
    }

    private void SendFriendsData(RequestFriendsDataMsg request)
    {
        if (server.TryGetFriendsData(request.playerID, out PlayerSocialData? friendsData))
        {
            Send(new FriendsDataMsg { success = true, data = friendsData });
            return;
        }

        Send(new FriendsDataMsg { success = false, error = "没有找到该玩家的好友数据。" });
    }

    private void HandleWorldChat(SendWorldChatMsg request)
    {
        if (currentPlayer == null)
        {
            SendWorldChatError("登录后才能发送世界消息。");
            return;
        }

        string content = request.content.Trim();
        if (content.Length == 0)
        {
            SendWorldChatError("消息内容不能为空。");
            return;
        }

        if (content.Length > 50)
        {
            SendWorldChatError("世界消息最多输入 50 个字符。");
            return;
        }

        WorldChatData data = server.CreateWorldChatData(currentPlayer, content);

        // 先记入历史再广播：之后登录的玩家从历史里拿到这条，已在线的玩家收到广播。
        server.RecordWorldChat(data);
        server.BroadcastWorldChat(new WorldChatMsg
        {
            success = true,
            data = data,
        });
    }

    private void SendWorldChatError(string error)
    {
        Send(new WorldChatMsg
        {
            success = false,
            error = error,
        });
    }
}
