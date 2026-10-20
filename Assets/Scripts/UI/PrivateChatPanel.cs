using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 复用为好友私聊和搜索玩家两种界面状态。
/// </summary>
public class PrivateChatPanel : BasePanel
{
    [SerializeField] private Button closeBtn;
    [SerializeField] private GameObject searchArea;
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button searchBtn;
    [SerializeField] private GameObject chatInputArea;
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private Button sendBtn;
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private RectTransform chatContent;
    [SerializeField] private VerticalLayoutGroup contentLayoutGroup;
    [SerializeField] private WorldChatItem ownMessagePrefab;
    [SerializeField] private WorldChatItem otherMessagePrefab;
    [SerializeField] private FriendRequestItem searchResultPrefab;

    private readonly List<GameObject> currentItems = new List<GameObject>();
    private FriendRelationData friendData;
    /// <summary>
    /// 是否为搜索状态
    /// </summary>
    private bool isSearchMode;

    private void Start()
    {
        closeBtn.onClick.AddListener(Close);
        searchBtn.onClick.AddListener(OnClickSearch);
        sendBtn.onClick.AddListener(OnClickSend);
    }

    public override void Open(object data = null)
    {
        base.Open(data);

        if (data is FriendRelationData selectedFriend)
        {
            OpenChatMode(selectedFriend);
            return;
        }

        OpenSearchMode();
    }

    public bool IsChattingWith(int playerID)
    {
        return !isSearchMode
            && friendData != null
            && friendData.friendPlayerID == playerID;
    }

    public void ShowSearchResult(PlayerProfileData playerData)
    {
        if (!isSearchMode)
        {
            return;
        }

        ClearCurrentItems();
        FriendRequestItem item = Instantiate(searchResultPrefab, chatContent);
        item.SetSearchResult(playerData);
        currentItems.Add(item.gameObject);
    }

    public void ClearSearchResult()
    {
        if (isSearchMode)
        {
            ClearCurrentItems();
        }
    }

    public void AddMessage(PrivateChatData data)
    {
        CreateMessageItem(data);
        ScrollToBottom();
    }

    private void OnClickSearch()
    {
        string account = searchInput.text.Trim();
        if (account.Length == 0)
        {
            return;
        }

        ClearCurrentItems();
        PlayerDataManager.Instance.SearchPlayer(account);
    }

    private void OnClickSend()
    {
        if (!PrivateChatManager.Instance.Send(friendData.friendPlayerID, chatInput.text))
        {
            return;
        }

        chatInput.text = string.Empty;
        chatInput.ActivateInputField();
    }

    private void RefreshMessages()
    {
        ClearCurrentItems();

        foreach (PrivateChatData data in PrivateChatManager.Instance.GetConversation(friendData.friendPlayerID))
        {
            CreateMessageItem(data);
        }

        ScrollToBottom();
    }

    private void CreateMessageItem(PrivateChatData data)
    {
        int currentPlayerID = PlayerDataManager.Instance.CurrentPlayerData.playerID;
        bool isOwnMessage = data.senderPlayerID == currentPlayerID;
        WorldChatItem prefab = isOwnMessage ? ownMessagePrefab : otherMessagePrefab;
        WorldChatItem item = Instantiate(prefab, chatContent);
        string senderAccount = isOwnMessage
            ? PlayerDataManager.Instance.CurrentPlayerData.account
            : friendData.playerData.account;

        item.SetData(senderAccount, data.content, data.sendTime);
        currentItems.Add(item.gameObject);
    }
    /// <summary>
    /// 搜索模式
    /// </summary>
    private void OpenSearchMode()
    {
        isSearchMode = true;
        friendData = null;
        contentLayoutGroup.childAlignment = TextAnchor.UpperLeft;
        searchArea.SetActive(true);
        chatInputArea.SetActive(false);
        searchInput.text = string.Empty;
        ClearCurrentItems();
        searchInput.ActivateInputField();
    }
    /// <summary>
    /// 聊天模式
    /// </summary>
    /// <param name="selectedFriend"></param>
    private void OpenChatMode(FriendRelationData selectedFriend)
    {
        isSearchMode = false;
        friendData = selectedFriend;
        contentLayoutGroup.childAlignment = TextAnchor.UpperCenter;
        searchArea.SetActive(false);
        chatInputArea.SetActive(true);
        RefreshMessages();
    }
        
    private void ClearCurrentItems()
    {
        foreach (GameObject item in currentItems)
        {
            Destroy(item);
        }

        currentItems.Clear();
    }

    private void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent);
        chatScrollRect.verticalNormalizedPosition = 0f;
    }
}
