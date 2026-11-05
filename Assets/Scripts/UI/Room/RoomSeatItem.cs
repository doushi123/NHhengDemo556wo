using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>显示房间中的一个席位，包括玩家账户名及房主和本人的标记。</summary>
public class RoomSeatItem : MonoBehaviour
{ 
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private GameObject ownerMark;
    [SerializeField] private Button seatBtn;

    // 此显示项对应的席位，用于点击空位时发送换座请求。
    private int seatIndex;

    /// <summary>绑定席位按钮，点击时请求移动到该席位。</summary>
    private void Awake()
    {
        seatBtn.onClick.AddListener(ChangeSeat);
    }

    /// <summary>刷新席位显示；玩家区域显示账户名，空位显示等待加入。</summary>
    public void Refresh(int seatIndex, PlayerProfileData player, bool isOwner, bool isSelf, bool canChange)
    {
        this.seatIndex = seatIndex;
        ownerMark.SetActive(isOwner);
        playerText.text = player == null ? "等待加入" : player.account;
        seatBtn.interactable = canChange && player == null;
    }

    /// <summary>只发送目标席位，不提前移动名字或标记，等待服务端确认。</summary>
    private void ChangeSeat()
    {
        RoomManager.Instance.ChangeSeat(seatIndex);
    }
}
