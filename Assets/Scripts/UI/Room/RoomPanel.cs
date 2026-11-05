using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>显示当前房间和四个席位，提供房主开始游戏、成员退出房间的入口。</summary>
public class RoomPanel : BasePanel
{
    [SerializeField] private TMP_Text roomText;
    // 当前真人玩家人数，例如 2/4；空席位不计入。
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button startBtn;
    [SerializeField] private Button leaveBtn;

    // 按席位 0、1、2、3 拖入四个显示项；收到房间数据后按相同顺序刷新。
    [SerializeField] private RoomSeatItem[] seats;

    /// <summary>检查四个席位的绑定数量，并绑定开始、退出按钮。</summary>
    private void Awake()
    {
        startBtn.onClick.AddListener(StartBattle);
        leaveBtn.onClick.AddListener(LeaveRoom);
    }

    /// <summary>创建、加入成功或成员变化时，显示服务端确认的最新房间数据。</summary>
    public override void Open(object data = null)
    {
        base.Open(data);
        Refresh();
    }

    /// <summary>刷新房间号、真人人数、成员和按钮状态；只有等待中的房主能点击开始。</summary>
    private void Refresh()
    {
        RoomData room = RoomManager.Instance.CurrentRoom;
        if (room == null)
        {
            throw new InvalidOperationException("尚未进入房间，不能打开 RoomPanel。");
        }

        int playerID = PlayerDataManager.Instance.CurrentPlayerData.playerID;
        bool isOwner = room.ownerPlayerID == playerID;
        roomText.text = "房间号：" + room.roomID;

        int playerCount = 0;
        for (int i = 0; i < seats.Length; i++)
        {
            PlayerProfileData player = room.seats[i];
            if (player != null)
            {
                playerCount++;
            }
            bool isOwnerSeat = player != null && player.playerID == room.ownerPlayerID;
            bool isSelfSeat = player != null && player.playerID == playerID;
            seats[i].Refresh(i, player, isOwnerSeat, isSelfSeat, room.state == RoomState.Waiting);
        }
        playerCountText.text = playerCount + "/" + BattleConst.SeatCount;

        startBtn.interactable = isOwner && room.state == RoomState.Waiting;
        leaveBtn.interactable = room.state == RoomState.Waiting;
    }

    /// <summary>发送开始请求，是否能开战以服务端响应为准。</summary>
    private void StartBattle()
    {
        RoomManager.Instance.StartBattle();
    }

    /// <summary>发送退出请求；收到退出确认前继续显示当前房间。</summary>
    private void LeaveRoom()
    {
        RoomManager.Instance.LeaveRoom();
    }
}
