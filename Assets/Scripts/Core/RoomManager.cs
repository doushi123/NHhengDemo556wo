using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 保存服务端确认的房间数据，并通过现有 TCP 通道发起房间操作。
/// 创建、加入、退出都以服务端响应为准，不提前修改本地席位。
/// 开战后依次安排场景加载、战斗连接、角色准备和就绪确认。
/// </summary>
public sealed class RoomManager : AppService
{
    public static RoomManager Instance { get; private set; }

    /// <summary>服务端最近下发的完整房间数据。</summary>
    public RoomData CurrentRoom { get; private set; }

    /// <summary>创建或加入请求正在等待响应，避免房间数据尚未返回时重复发送入房请求。</summary>
    private bool entering;

    /// <summary>正在执行的入场协程，取消或断线时直接停止，不再继续连接和上报就绪。</summary>
    private Coroutine battleLoading;

    protected override void OnInitialize()
    {
        Instance = this;
    }

    /// <summary>只上传所选玩法模式。</summary>
    public void CreateRoom(BattleMode mode)
    {
        if (!CanEnter())
        {
            return;
        }

        // 未连接时由 NetManager 显示提示，不进入等待响应的状态。
        entering = NetManager.Instance.IsConnected;
        NetManager.Instance.Send(new CreateRoomRequestMsg { mode = mode });
    }

    /// <summary>通过房间号加入，席位由服务端分配。</summary>
    public void JoinRoom(int roomID)
    {
        if (!CanEnter())
        {
            return;
        }

        entering = NetManager.Instance.IsConnected;
        NetManager.Instance.Send(new JoinRoomRequestMsg { roomID = roomID });
    }

    /// <summary>在客户端提示先退出当前房间，或等待正在处理的入房请求。</summary>
    private bool CanEnter()
    {
        if (CurrentRoom != null)
        {
            UIManager.Instance.Open(PanelId.Tip, "请先离开当前房间。");
            return false;
        }
        if (entering)
        {
            UIManager.Instance.Open(PanelId.Tip, "正在进入房间，请稍候。");
            return false;
        }
        return true;
    }

    /// <summary>请求离开当前房间，收到退出确认后才清空本地数据。</summary>
    public void LeaveRoom()
    {
        NetManager.Instance.Send(new LeaveRoomRequestMsg());
    }

    /// <summary>房主点击开始游戏后，请求服务端开始当前房间的战斗。</summary>
    public void StartBattle()
    {
        NetManager.Instance.Send(new StartBattleRequestMsg());
    }

    /// <summary>请求换到指定席位，收到服务端房间数据后再更新显示。</summary>
    public void ChangeSeat(int seatIndex)
    {
        NetManager.Instance.Send(new ChangeSeatRequestMsg { seatIndex = seatIndex });
    }

    /// <summary>创建、加入或其他成员变化后，替换本地房间数据。</summary>
    public void Receive(RoomDataMsg msg)
    {
        CurrentRoom = msg.data;
        entering = false;
        if (CurrentRoom.state == RoomState.Waiting)
        {
            if (!UIManager.Instance.IsOpen(PanelId.Lobby))
            {
                UIManager.Instance.Open(PanelId.Lobby);
            }
            UIManager.Instance.GetOpenPanel<LobbyPanel>(PanelId.Lobby).CloseJoinRoom();
            UIManager.Instance.Open(PanelId.Room);
        }
        else
        {
            UIManager.Instance.Close(PanelId.Room);
        }
    }

    /// <summary>收到服务端退出确认，清空当前房间。</summary>
    public void Receive(RoomLeftMsg msg)
    {
        Clear();
    }

    /// <summary>房间操作失败时显示服务端原因，保留原来已经确认的房间数据。</summary>
    public void Receive(OperationResultMsg msg)
    {
        if (msg.operation == OperationType.CreateRoom || msg.operation == OperationType.JoinRoom)
        {
            entering = false;
        }
        if (!msg.success)
        {
            UIManager.Instance.Open(PanelId.Tip, msg.error);
        }
    }

    /// <summary>收到正式开战通知后，加载地图场景并开始本房间的战斗入场。</summary>
    public void Receive(BattleEntryMsg msg)
    {
        battleLoading = StartCoroutine(LoadBattle(msg));
    }

    /// <summary>取消当前房间的入场或战斗，关闭加载界面并显示服务端原因。</summary>
    public void Receive(BattleCancelledMsg msg)
    {
        StopBattleLoading();
        BattleNetManager.Instance.Close();
        UIManager.Instance.Open(PanelId.Tip, msg.error);
    }

    /// <summary>按本机阵营显示胜负，清理本局并返回大厅，保留登录和 TCP 连接。</summary>
    public void Receive(BattleFinishedMsg msg)
    {
        BattleCamp ownCamp = BattleNetManager.Instance.InitData.Value.seats[BattleNetManager.Instance.SeatIndex].camp;
        string result = msg.winningCamp == 0 ? "双方秘核同时被摧毁，本局平局。" : msg.winningCamp == (int)ownCamp ? "我方阵营胜利！" : "敌方阵营胜利！";
        Clear();
        UIManager.Instance.Open(PanelId.Lobby);
        LoaderManager.Instance.LoadScene("BootStrap");
        UIManager.Instance.Open(PanelId.Tip, result);
    }

    /// <summary>借助全局加载器切场景，再连接 KCP、准备角色并等待全员就绪后统一开始。</summary>
    private IEnumerator LoadBattle(BattleEntryMsg entry)
    {
        UIManager.Instance.Close(PanelId.Lobby);
        UIManager.Instance.Close(PanelId.Room);
        UIManager.Instance.Close(PanelId.Option);
        UIManager.Instance.Close(PanelId.Chat);

        try
        {
            if (entry.mapID != 1)
            {
                throw new InvalidOperationException("未配置地图 " + entry.mapID + " 对应的场景。");
            }
            // 场景占总进度的 80%；剩余准备和全员等待由房间流程接着处理。
            LoaderManager.Instance.LoadScene("BeginScene", false, 0.8f);
        }
        catch (Exception exception)
        {
            ReportLoadFailure(entry.battleID, exception.Message);
            yield break;
        }

        while (LoaderManager.Instance.IsSceneLoading)
        {
            yield return null;
        }
        if (!string.IsNullOrEmpty(LoaderManager.Instance.Error))
        {
            ReportLoadFailure(entry.battleID, LoaderManager.Instance.Error);
            yield break;
        }

        LoaderManager.Instance.SetProgress(0.8f, "守护己方秘核，攻破敌方核心。");
        BattleNetManager.Instance.Connect(entry.battleToken);
        // 全员加载期限由服务端统一计时，超时会收到取消通知；TCP 断线则由 Clear 停止协程。
        while (BattleNetManager.Instance.Simulation == null)
        {
            if (!string.IsNullOrEmpty(BattleNetManager.Instance.Error))
            {
                ReportLoadFailure(entry.battleID, BattleNetManager.Instance.Error);
                yield break;
            }
            yield return null;
        }

        try
        {
            PlayerController players = FindObjectOfType<PlayerController>();
            players.PrepareBattle();
            BattleNetManager.Instance.SendReady();
        }
        catch (Exception exception)
        {
            ReportLoadFailure(entry.battleID, exception.Message);
            yield break;
        }

        LoaderManager.Instance.SetProgress(1f, "战场集结中，等待其他英雄抵达。");
        while (!BattleNetManager.Instance.IsRunning)
        {
            if (!string.IsNullOrEmpty(BattleNetManager.Instance.Error))
            {
                ReportLoadFailure(entry.battleID, BattleNetManager.Instance.Error);
                yield break;
            }
            yield return null;
        }
        battleLoading = null;
        LoaderManager.Instance.CloseLoading();
    }

    /// <summary>记录入场失败并通知服务端取消本局，成员收到取消通知后统一显示原因。</summary>
    private void ReportLoadFailure(long battleID, string error)
    {
        Debug.LogError("进入战斗失败：" + error);
        BattleNetManager.Instance.Close();
        NetManager.Instance.Send(new BattleLoadFailedMsg { battleID = battleID, error = error });
        LoaderManager.Instance.CloseLoading();
    }

    /// <summary>停止本房间的入场协程并关闭它使用的 Loading；没有入场时不影响其他加载流程。</summary>
    private void StopBattleLoading()
    {
        if (battleLoading == null)
        {
            return;
        }
        StopCoroutine(battleLoading);
        battleLoading = null;
        LoaderManager.Instance.CloseLoading();
    }

    /// <summary>主动退出或 TCP 断线时清空房间，由主线程调用。</summary>
    public void Clear()
    {
        StopBattleLoading();
        BattleNetManager.Instance.Close();
        // 退出确认或断线时关闭房间界面；大厅一直保留在下层。
        UIManager.Instance.Close(PanelId.Room);
        UIManager.Instance.GetOpenPanel<LobbyPanel>(PanelId.Lobby)?.CloseJoinRoom();
        CurrentRoom = null;
        entering = false;
    }

    /// <summary>关闭服务时清空缓存并释放静态入口。</summary>
    protected override void OnShutdown()
    {
        Clear();
        Instance = null;
    }
}
