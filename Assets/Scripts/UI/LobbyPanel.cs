using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPanel : BasePanel
{
    [SerializeField]
    private Button exitBtn;
    [SerializeField]
    private Button friendBtn;
    [SerializeField] private GameObject chatBg;
    [SerializeField] private Button chatBtn;
    [SerializeField] private Button chatWorldCloseBtn;
    [SerializeField]private Button[] modeBtns;

    [Header("世界聊天")]
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private Button sendBtn;

    [SerializeField] private ScrollRect worldChatScrollRect;
    [SerializeField] private RectTransform worldChatContent;
    [SerializeField] private WorldChatItem ownMessagePrefab;
    [SerializeField] private WorldChatItem otherMessagePrefab;

    private readonly List<WorldChatItem> messageItems = new List<WorldChatItem>();

    private void Start()
    {
        chatBg.SetActive(false);
        exitBtn.onClick.AddListener(() =>
        {
            Application.Quit();
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
        foreach(var modeBtn in modeBtns)
        {
            
        }
    }

    public override void Open(object data = null)
    {
        base.Open(data);
        RefreshWorldChat();
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
        LayoutRebuilder.ForceRebuildLayoutImmediate(worldChatContent);
        worldChatScrollRect.verticalNormalizedPosition = 0f;
    }

}
