using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>输入房间号并请求加入房间，服务端确认成功后再关闭。</summary>
public class JoinRoomPanel : BasePanel
{
    [SerializeField] private TMP_InputField roomInput;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button closeBtn;

    /// <summary>绑定确认和关闭按钮，并将房间号设为整数输入。</summary>
    private void Awake()
    {
        confirmBtn.onClick.AddListener(JoinRoom);
        closeBtn.onClick.AddListener(Close);
        roomInput.contentType = TMP_InputField.ContentType.IntegerNumber;
    }

    /// <summary>每次打开都清空房间号，并让输入框获得焦点。</summary>
    public override void Open(object data = null)
    {
        base.Open(data);
        roomInput.text = string.Empty;
        roomInput.ActivateInputField();
    }

    /// <summary>检查房间号并发送加入请求，输入错误或服务端拒绝时保留界面以便修改。</summary>
    private void JoinRoom()
    {
        if (!int.TryParse(roomInput.text.Trim(), out int roomID) || roomID <= 0)
        {
            UIManager.Instance.Open(PanelId.Tip, "请输入有效的房间号。");
            return;
        }

        RoomManager.Instance.JoinRoom(roomID);
    }
}
