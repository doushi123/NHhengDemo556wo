using System;
using System.IO;
using kcp2k;
using UnityEngine;

/// <summary>
/// 管理客户端与战斗服务端之间的 KCP 连接。
/// 负责战斗认证、输入上传、权威操作帧接收及客户端确定性模拟。
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
    /// <summary>角色提交的最新操作；网络按固定频率发送这份输入，不持有角色组件。</summary>
    private BattleInput currentInput;
    private BattleSimulation simulation;
    private int nextInputFrame;
    private double nextInputAt;
    // 正式流程等场景与角色准备好后，由 RoomManager 的入场流程明确上报；测试入口在已有场景中准备。
    private bool waitForSceneReady;
    private bool readySent;

    /// <summary>收到服务端统一开始通知后才允许采集输入、发送操作与执行帧。</summary>
    public bool IsRunning { get; private set; }

    /// <summary>本次连接或初始化失败的原因，供加载入口显示并通知服务端。</summary>
    public string Error { get; private set; }
    /// <summary>
    /// 仅测试链路使用：缓存已上传服务端的正式地图 JSON（去掉缩进后的文本），供同文本 Hash 校验。
    /// 正式链路不设置此字段，直接读取本地导出的地图文件。
    /// </summary>
    private string uploadedMapJson;
    /// <summary>
    /// 当前是否通过认证
    /// </summary>
    public bool IsAuthenticated { get; private set; }
    /// <summary>
    /// 当前客户端控制四个英雄席位中的哪一个
    /// </summary>
    public int SeatIndex { get; private set; }
    public BattleInitData?  InitData { get; private set; }
    public BattleSimulation Simulation => simulation;

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
        SendCurrentInput();
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
    public void Connect(string token, bool waitForSceneReady = false)
    {
        Close();

        this.waitForSceneReady = waitForSceneReady;
        Error = null;
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
        simulation = null;
        IsRunning = false;
        readySent = false;
        nextInputFrame = 0;
        currentInput = default;
        uploadedMapJson = null;
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
                    BeginBattle(BattlePacketCodec.Deserialize<BattleInitData>(bytes));
                    break;

                case BattlePacketType.AuthorityFrame:
                    if (!IsRunning)
                    {
                        throw new InvalidDataException("尚未收到统一开始通知，却收到了操作帧。");
                    }
                    simulation.Step(BattlePacketCodec.Deserialize<AuthorityFramePacket>(bytes));
                    if (!waitForSceneReady && simulation.Frame == 0)
                    {
                        Debug.Log("测试战斗收到首个权威帧，角色移动可以开始。");
                    }
                    break;

                case BattlePacketType.Start:
                    BattleStartPacket start = BattlePacketCodec.Deserialize<BattleStartPacket>(bytes);
                    if (!readySent || !InitData.HasValue || start.battleID != InitData.Value.battleID)
                    {
                        throw new InvalidDataException("开始通知与已准备好的战斗不一致。");
                    }
                    IsRunning = true;
                    nextInputAt = Time.realtimeSinceStartupAsDouble;
                    break;

                default:
                    Debug.LogWarning("客户端未处理的战斗消息：" + type);
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail("战斗初始化或消息处理失败：" + exception.Message);
        }
    }

    private void ReceiveAuthResult(BattleAuthResultPacket packet)
    {
        if (packet.result != BattleAuthResult.Success)
        {
            Fail("战斗不存在或已经结束。");
            return;
        }

        IsAuthenticated = true;
        SeatIndex = packet.seatIndex;
    }

    /// <summary>接收本地角色采集的操作，只更新缓存，不立即发送或推进模拟。</summary>
    public void SetInput(BattleInput input)
    {
        currentInput = input;
    }

    /// <summary>场景、模拟和本地角色都准备好后调用，只上报一次，不在本机提前开帧。</summary>
    public void SendReady()
    {
        if (readySent)
        {
            return;
        }
        if (!IsAuthenticated || simulation == null)
        {
            throw new InvalidOperationException("战斗认证与模拟初始化完成后才能上报就绪。");
        }
        Send(BattlePacketCodec.Serialize(new BattleReadyPacket()));
        readySent = true;
    }

    /// <summary>仅供测试入口调用：保存已上传的正式地图 JSON，避免排版差异导致 Hash 不一致；正式链路不调用。</summary>
    public void SetUploadedMapForTest(string mapJson)
    {
        uploadedMapJson = mapJson;
    }

    /// <summary>读取双端相同的配置和地图文件，并在第一帧前核对 Hash。</summary>
    private void BeginBattle(BattleInitData init)
    {
        string configJson = File.ReadAllText(Path.Combine(Application.streamingAssetsPath,
            BattleConst.ConfigFileName));
        // 测试入口使用已上传的同一段文本；正式链路读取本地正式导出文件。
        string mapJson = BattleTestMode.Enabled && uploadedMapJson != null? uploadedMapJson
            : File.ReadAllText(Path.Combine(Application.streamingAssetsPath,
                "Maps", "Map_" + init.mapID + ".json"));
        if (BattleConfigSerializer.GetHash(configJson) != init.configHash ||
            BattleConfigSerializer.GetHash(mapJson) != init.mapHash)
        {
            throw new InvalidDataException("客户端的战斗配置或地图与服务端不同。");
        }
        simulation = new BattleSimulation(init, new BattlePhysics(MapSerializer.Deserialize(mapJson)),
            BattleConfigSerializer.Deserialize(configJson));
        InitData = init;
        nextInputFrame = BattleConst.InputDelayFrames;
        nextInputAt = Time.realtimeSinceStartupAsDouble;
        if (!waitForSceneReady)
        {
            // 自动测试入口已经位于战斗场景，但同样必须完成角色准备并明确上报就绪。
            FindObjectOfType<PlayerController>().PrepareBattle();
            SendReady();
        }
        if (!waitForSceneReady)
        {
            Debug.Log("测试战斗初始化完成：战斗 " + init.battleID + "，地图 " + init.mapID + "。");
        }
    }

    /// <summary>以固定逻辑频率把当前按键采样发到未来帧；指定它在哪个未来帧生效。</summary>
    private void SendCurrentInput()
    {
        if (!IsRunning || Time.realtimeSinceStartupAsDouble < nextInputAt)
        {
            return;
        }
        int targetFrame = Math.Max(nextInputFrame,
            simulation.Frame + BattleConst.InputDelayFrames + 1);
        SendInput(new PlayerInputPacket
        {
            targetFrame = targetFrame,
            input = currentInput,
        });
        nextInputFrame = targetFrame + 1;
        //下次至少发送后面一帧
        nextInputAt = Time.realtimeSinceStartupAsDouble + 1.0 / BattleConst.LogicFrameRate;
    }

    private void OnDisconnected()
    {
        // Close 主动断开旧连接时，不把清理动作当成新的网络故障。
        if (client == null)
        {
            return;
        }
        if (!shouldClose)
        {
            Fail("战斗连接已断开。");
        }
        shouldClose = true;
    }

    private void OnError(ErrorCode error, string message)
    {
        Debug.LogWarning("KCP 战斗连接错误：" + error + "，" + message);
        Fail("战斗连接发生错误：" + message);
    }

    /// <summary>保留具体错误供入场协程读取，KCP 更新结束后再关闭连接。</summary>
    private void Fail(string error)
    {
        Error = error;
        shouldClose = true;
        Debug.LogError(error);
        if (!waitForSceneReady)
        {
            UIManager.Instance.Open(PanelId.Tip, error);
        }
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
