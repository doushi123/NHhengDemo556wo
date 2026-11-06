using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPanel : BasePanel
{
    [SerializeField] private Button exitBtn;
    [SerializeField] private Button friendBtn;
    [SerializeField] private Button sideCloseBtn;
    [SerializeField] private GameObject FriendSideBg;
    [SerializeField] private FriendSidePanel friendSidePanel;
    [SerializeField] private GameObject chatBg;
    [SerializeField] private Button chatBtn;
    [SerializeField] private Button chatWorldCloseBtn;
    // 按大厅卡片顺序绑定：第一个进入核心玩法，其余只提示“敬请期待”。
    [SerializeField] private Button[] modeBtns;

    [Header("加入房间")]
    [SerializeField] private GameObject joinRoomBg;
    [SerializeField] private TMP_InputField roomInput;
    [SerializeField] private Button joinRoomBtn;
    [SerializeField] private Button joinRoomCloseBtn;

    [Header("世界聊天")]
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private Button sendBtn;

    [SerializeField] private ScrollRect worldChatScrollRect;
    [SerializeField] private RectTransform worldChatContent;
    [SerializeField] private WorldChatItem ownMessagePrefab;
    [SerializeField] private WorldChatItem otherMessagePrefab;

    private List<WorldChatItem> messageItems = new List<WorldChatItem>();

    private void Start()
    {
        roomInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        joinRoomBtn.onClick.AddListener(JoinRoom);
        joinRoomCloseBtn.onClick.AddListener(CloseJoinRoom);
        chatBg.SetActive(false);
        FriendSideBg.SetActive(false);
        exitBtn.onClick.AddListener(OpenExitOptions);
        friendBtn.onClick.AddListener(() =>
        {
            FriendSideBg.SetActive(true);
        });
        sideCloseBtn.onClick.AddListener(() =>
        {
            FriendSideBg.SetActive(false);
            UIManager.Instance.Close(PanelId.Chat);
        });
        sendBtn.onClick.AddListener(OnClickSendWorldChat);
        chatBtn.onClick.AddListener(() =>
        {
            chatBg.SetActive(true);
        });
        chatWorldCloseBtn.onClick.AddListener(() =>
        {
           chatBg.SetActive(false); 
        });
        modeBtns[0].onClick.AddListener(() => OpenRoomOptions(BattleMode.CoreWar));
        for (int i = 1; i < modeBtns.Length; i++)
        {
            modeBtns[i].onClick.AddListener(() => UIManager.Instance.Open(PanelId.Tip, "敬请期待"));
        }
    }

    /// <summary>点击玩法卡片后，打开创建或加入房间的选项框。</summary>
    private void OpenRoomOptions(BattleMode mode)
    {
        if (RoomManager.Instance.CurrentRoom != null)
        {
            UIManager.Instance.Open(PanelId.Room);
            return;
        }

        OptionPanel panel = (OptionPanel)UIManager.Instance.Open(PanelId.Option);
        panel.OpenRoomOptions(mode);
    }

    /// <summary>复用选项弹窗确认退出，取消或关闭弹窗时继续留在大厅。</summary>
    private void OpenExitOptions()
    {
        OptionPanel panel = (OptionPanel)UIManager.Instance.Open(PanelId.Option);
        panel.OpenQuitOptions();
    }

    public override void Open(object data = null)
    {
        base.Open(data);
        CloseJoinRoom();
        RefreshWorldChat();
    }

    /// <summary>关闭大厅时一并隐藏房间号输入区域。</summary>
    public override void Close()
    {
        CloseJoinRoom();
        base.Close();
    }

    /// <summary>显示大厅内的房间号输入区域，清空旧内容并让输入框获得焦点。</summary>
    public void OpenJoinRoom()
    {
        joinRoomBg.SetActive(true);
        roomInput.text = string.Empty;
        roomInput.ActivateInputField();
    }

    /// <summary>取消输入或成功入房后，隐藏房间号输入区域。</summary>
    public void CloseJoinRoom()
    {
        joinRoomBg.SetActive(false);
    }

    /// <summary>检查房间号并请求加入；输入错误或服务端拒绝时保留输入区域。</summary>
    private void JoinRoom()
    {
        if (!int.TryParse(roomInput.text.Trim(), out int roomID) || roomID <= 0)
        {
            UIManager.Instance.Open(PanelId.Tip, "请输入有效的房间号。");
            return;
        }

        RoomManager.Instance.JoinRoom(roomID);
    }

    private void OnClickSendWorldChat()
    {
        if (!WorldChatManager.Instance.Send(chatInput.text))
        {
            return;
        }

        chatInput.text = string.Empty;
        chatInput.ActivateInputField();
    }

    /// <summary>
    /// 大厅打开时，把登录阶段已经下发的世界聊天历史生成出来。
    /// </summary>
    private void RefreshWorldChat()
    {
        foreach (WorldChatItem item in messageItems)
        {
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }

        messageItems.Clear();

        foreach (WorldChatData data in WorldChatManager.Instance.Messages)
        {
            CreateWorldChatItem(data);
        }

        ScrollWorldChatToBottom();
    }

    /// <summary>
    /// 大厅打开期间收到实时广播时，追加一条消息 UI。
    /// 消息数据已经先由 WorldChatManager 保存，这里只负责显示。
    /// </summary>
    public void AddWorldChatMessage(WorldChatData data)
    {
        CreateWorldChatItem(data);

        if (messageItems.Count > NetConst.MaxWorldChatCount)
        {
            WorldChatItem oldestItem = messageItems[0];
            messageItems.RemoveAt(0);
            oldestItem.gameObject.SetActive(false);
            Destroy(oldestItem.gameObject);
        }

        ScrollWorldChatToBottom();
    }

    /// <summary>
    /// 好友数据变化后刷新侧边栏当前正在显示的页签。
    /// </summary>
    public void RefreshFriendSidePanel()
    {
        friendSidePanel.RefreshCurrentPage();
    }

    private void CreateWorldChatItem(WorldChatData data)
    {
        bool isOwnMessage = data.senderPlayerID == PlayerDataManager.Instance.CurrentPlayerData.playerID;
        WorldChatItem prefab = isOwnMessage ? ownMessagePrefab : otherMessagePrefab;
        WorldChatItem item = Instantiate(prefab, worldChatContent);
        item.SetData(data);
        messageItems.Add(item);
    }

    private void ScrollWorldChatToBottom()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(worldChatContent);
        worldChatScrollRect.verticalNormalizedPosition = 0f;
    }

}
