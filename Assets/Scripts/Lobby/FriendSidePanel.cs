using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FriendSidePanel : MonoBehaviour
{
    [SerializeField]
    private Button friendsBtn;
    [SerializeField]
    private Button friendRqsBtn;

    [SerializeField]
    private Image friendsSprite;
    [SerializeField]
    private Image friendsRqsSprite;

    [SerializeField]
    private RectTransform content;
    [SerializeField]
    private FriendListItem friendListItemPrefab;
    [SerializeField]
    private FriendRequestItem friendRequestItemPrefab;

    private readonly List<GameObject> currentItems = new List<GameObject>();
    private bool isShowingRequests;
    private bool initialized;

    private void Start()
    {
        friendsBtn.onClick.AddListener(OnClickFriends);
        friendRqsBtn.onClick.AddListener(OnClickFriendRequests);

        initialized = true;
        ShowFriends();
    }

    /// <summary>
    /// 好友数据更新后，保持当前页签并重新生成列表。
    /// </summary>
    public void RefreshCurrentPage()
    {
        if (!initialized)
        {
            return;
        }

        if (isShowingRequests)
        {
            ShowFriendRequests();
            return;
        }

        ShowFriends();
    }

    /// <summary>
    /// 切到好友页签时先关闭右侧面板，等待玩家选择具体好友后再打开聊天。
    /// </summary>
    private void OnClickFriends()
    {
        ShowFriends();
        UIManager.Instance.Close(PanelId.Chat);
    }

    /// <summary>
    /// 显示当前玩家已有的好友。
    /// </summary>
    private void ShowFriends()
    {
        isShowingRequests = false;
        friendsSprite.enabled = true;
        friendsRqsSprite.enabled = false;
        ClearCurrentItems();

        foreach (FriendRelationData friendData in PlayerDataManager.Instance.CurrentFriendsData.friends)
        {
            FriendListItem item = Instantiate(friendListItemPrefab, content);
            item.SetData(friendData);
            currentItems.Add(item.gameObject);
        }
    }

    /// <summary>
    /// 切到好友申请页签，并在右侧打开玩家搜索界面。
    /// </summary>
    private void OnClickFriendRequests()
    {
        ShowFriendRequests();
        UIManager.Instance.Open(PanelId.Chat);
    }

    /// <summary>
    /// 显示当前玩家尚未处理的好友申请。
    /// </summary>
    private void ShowFriendRequests()
    {
        isShowingRequests = true;
        friendsSprite.enabled = false;
        friendsRqsSprite.enabled = true;
        ClearCurrentItems();

        foreach (FriendRequestData requestData in PlayerDataManager.Instance.CurrentFriendsData.friendRequests)
        {
            FriendRequestItem item = Instantiate(friendRequestItemPrefab, content);
            item.SetData(requestData);
            currentItems.Add(item.gameObject);
        }
    }

    private void ClearCurrentItems()
    {
        foreach (GameObject item in currentItems)
        {
            Destroy(item);
        }

        currentItems.Clear();
    }
}
