using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public sealed class NetManager : AppService
{
    public static NetManager Instance { get; private set; }

    private readonly ConcurrentQueue<BaseMsg> sendMsgQueue = new ConcurrentQueue<BaseMsg>();
    private readonly ConcurrentQueue<BaseMsg> receiveMsgQueue = new ConcurrentQueue<BaseMsg>();
    /// <summary>
    /// 用于唤醒线程
    /// </summary>
    private readonly AutoResetEvent sendSignal = new AutoResetEvent(false);

    private readonly byte[] receiveBytes = new byte[8192];
    private readonly byte[] cacheBytes = new byte[NetConst.MaxMessageBodySize + 8];

    private Socket socket;
    private Thread sendThread;
    private Thread receiveThread;
    private int cacheNum;
    private volatile bool isConnected;
    private string pendingTip;

    public bool IsConnected => isConnected;

    protected override void OnInitialize()
    {
        Instance = this;

        // 启动时扫描并缓存本程序集中的所有 NetMessage。
        NetSerializer.RegisterMessages(typeof(NetManager).Assembly);
    }

    private void Update()
    {
        while (receiveMsgQueue.TryDequeue(out BaseMsg msg))
        {
            HandleMessage(msg);
        }

        string tip = Interlocked.Exchange(ref pendingTip, null);
        if (!string.IsNullOrEmpty(tip))
        {
            UIManager.Instance.Open(PanelId.Tip, tip);
        }
    }

    public void Connect()
    {
        if (isConnected)
        {
            return;
        }

        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            socket.Connect(new IPEndPoint(IPAddress.Parse(NetConst.ServerIP), NetConst.ServerPort));
            isConnected = true;

            sendThread = new Thread(SendMessages) { IsBackground = true };
            receiveThread = new Thread(ReceiveMessages) { IsBackground = true };
            sendThread.Start();
            receiveThread.Start();
        }
        catch (SocketException)
        {
            Close();
            UIManager.Instance.Open(PanelId.Tip, "无法连接服务器，请确认服务器已经启动。");
        }
    }

    public void Login(string account, string password)
    {
        Send(new LoginRequestMsg
        {
            account = account,
            password = password,
        });
    }

    public void Register(string account, string password)
    {
        Send(new RegisterRequestMsg
        {
            account = account,
            password = password,
        });
    }

    public void Send(BaseMsg msg)
    {
        if (!isConnected)
        {
            UIManager.Instance.Open(PanelId.Tip, "尚未连接到服务器。");
            return;
        }

        sendMsgQueue.Enqueue(msg);
        sendSignal.Set();
    }

    public void Close()
    {
        isConnected = false;
        sendSignal.Set();

        if (socket != null)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }

            socket.Close();
            socket = null;
        }

        cacheNum = 0;
    }

    protected override void OnShutdown()
    {
        Close();
        Instance = null;
    }

    private void SendMessages()
    {
        try
        {
            while (isConnected)
            {
                sendSignal.WaitOne();

                while (isConnected && sendMsgQueue.TryDequeue(out BaseMsg msg))
                {
                    // 消息类只保存数据，统一由 NetSerializer 转成完整网络包。
                    SendAll(NetSerializer.Serialize(msg));
                }
            }
        }
        catch (Exception)
        {
            DisconnectFromWorker("与服务器的连接已中断。");
        }
    }

    private void ReceiveMessages()
    {
        try
        {
            while (isConnected)
            {
                int receiveNum = socket.Receive(receiveBytes);
                if (receiveNum == 0)
                {
                    DisconnectFromWorker("服务端已断开连接。");
                    return;
                }

                HandleReceiveMessages(receiveBytes, receiveNum);
            }
        }
        catch (Exception)
        {
            if (isConnected)
            {
                DisconnectFromWorker("与服务器的连接已中断。");
            }
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

            if (bodyLength < 0 || bodyLength > NetConst.MaxMessageBodySize)
            {
                throw new InvalidOperationException("非法消息长度：" + bodyLength);
            }

            int messageLength = 8 + bodyLength;
            if (cacheNum - index < messageLength)
            {
                break;
            }

            // 公共消息头已经解析完，这里只把消息体恢复成具体消息对象。
            BaseMsg msg = NetSerializer.Deserialize(msgID, cacheBytes, index + 8, bodyLength);
            if (msg != null)
            {
                receiveMsgQueue.Enqueue(msg);
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
        if (msg is LoginResponseMsg loginResponse)
        {
            ApplyAccountResult("登录", loginResponse.success, loginResponse.error, loginResponse.initialData);
            return;
        }

        if (msg is RegisterResponseMsg registerResponse)
        {
            ApplyAccountResult("注册", registerResponse.success, registerResponse.error, registerResponse.initialData);
            return;
        }

        if (msg is PlayerDataMsg playerDataMsg)
        {
            PlayerDataManager.Instance.Receive(playerDataMsg);
            return;
        }

        if (msg is FriendsDataMsg friendsDataMsg)
        {
            PlayerDataManager.Instance.Receive(friendsDataMsg);
            return;
        }

        if (msg is WorldChatMsg worldChatMsg)
        {
            WorldChatManager.Instance.Receive(worldChatMsg);
        }
    }

    private void ApplyAccountResult(
        string operationName,
        bool success,
        string error,
        LoginInitialData initialData)
    {
        if (!success)
        {
            UIManager.Instance.Open(PanelId.Tip, error);
            return;
        }

        ApplyLoginInitialData(initialData);
        UIManager.Instance.Close(PanelId.Login);
        UIManager.Instance.Open(PanelId.Lobby);
        Debug.Log($"{operationName}成功：{initialData.playerData.account}");
    }

    /// <summary>
    /// 将登录后统一下发的首批数据交给对应的客户端管理器。
    /// </summary>
    private void ApplyLoginInitialData(LoginInitialData initialData)
    {
        PlayerDataManager.Instance.ApplyLoginData(initialData.playerData, initialData.socialData);
        WorldChatManager.Instance.ApplyHistory(initialData.worldChatHistory);
    }

    private void SendAll(byte[] bytes)
    {
        int sentNum = 0;
        while (sentNum < bytes.Length)
        {
            sentNum += socket.Send(bytes, sentNum, bytes.Length - sentNum, SocketFlags.None);
        }
    }

    private void DisconnectFromWorker(string tip)
    {
        pendingTip = tip;
        Close();
    }
}
