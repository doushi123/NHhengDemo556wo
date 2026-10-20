using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 好友列表中的一条好友信息。
/// </summary>
public class FriendListItem : MonoBehaviour
{
    [SerializeField]
    private TMP_Text accountText;
    [SerializeField]
    private Button chatBtn;
    [SerializeField]
    private Button inviteBtn;

    private FriendRelationData friendData;

    private void Start()
    {
        chatBtn.onClick.AddListener(OnClickChat);
        inviteBtn.onClick.AddListener(OnClickInvite);
    }

    public void SetData(FriendRelationData data)
    {
        friendData = data;
        accountText.text = data.playerData.account;
    }

    private void OnClickChat()
    {
        UIManager.Instance.Open(PanelId.Chat, friendData);
    }

    private void OnClickInvite()
    {
        UIManager.Instance.Open(PanelId.Tip, $"邀请 {friendData.playerData.account} 进入房间的功能尚未接入。");
    }
}
