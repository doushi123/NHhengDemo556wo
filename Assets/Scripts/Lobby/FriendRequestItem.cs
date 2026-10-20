using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 复用为待处理好友申请和玩家搜索结果。
/// </summary>
public class FriendRequestItem : MonoBehaviour
{
    [SerializeField]
    private TMP_Text accountText;
    [SerializeField]
    private Button acceptBtn;
    [SerializeField]
    private Button rejectBtn;
    [SerializeField]
    private Button sendRequestBtn;

    private FriendRequestData requestData;
    private PlayerProfileData searchPlayerData;

    private void Start()
    {
        acceptBtn.onClick.AddListener(OnClickAccept);
        rejectBtn.onClick.AddListener(OnClickReject);
        sendRequestBtn.onClick.AddListener(OnClickSendRequest);
    }
    /// <summary>
    /// 显示待未处理的好友申请时
    /// </summary>
    /// <param name="data"></param>
    public void SetData(FriendRequestData data)
    {
        requestData = data;
        searchPlayerData = null;
        accountText.text = data.senderData.account;
        acceptBtn.gameObject.SetActive(true);
        rejectBtn.gameObject.SetActive(true);
        sendRequestBtn.gameObject.SetActive(false);
    }
    /// <summary>
    /// 显示搜索结果
    /// </summary>
    /// <param name="data"></param>
    public void SetSearchResult(PlayerProfileData data)
    {
        requestData = null;
        searchPlayerData = data;
        accountText.text = data.account;
        acceptBtn.gameObject.SetActive(false);
        rejectBtn.gameObject.SetActive(false);
        sendRequestBtn.gameObject.SetActive(true);
    }

    private void OnClickAccept()
    {
        PlayerDataManager.Instance.HandleFriendRequest(requestData.senderPlayerID, true);
    }

    private void OnClickReject()
    {
        PlayerDataManager.Instance.HandleFriendRequest(requestData.senderPlayerID, false);
    }

    private void OnClickSendRequest()
    {
        PlayerDataManager.Instance.SendFriendRequest(searchPlayerData.playerID);
    }
}
