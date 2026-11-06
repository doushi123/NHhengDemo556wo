using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>通用双选项弹窗，具体业务通过公开入口方法配置。</summary>
public class OptionPanel : BasePanel
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text firstText;
    [SerializeField] private TMP_Text secondText;
    [SerializeField] private Button firstBtn;
    [SerializeField] private Button secondBtn;
    [SerializeField] private Button closeBtn;

    // 创建房间时暂存大厅选择的玩法，点击创建按钮时直接交给 RoomManager。
    private BattleMode roomMode;

    /// <summary>绑定关闭按钮；两个选项按钮由具体的公开入口方法绑定。</summary>
    private void Awake()
    {
        closeBtn.onClick.AddListener(Close);
    }

    /// <summary>打开进入房间的选项，按钮直接执行创建或加入逻辑。</summary>
    public void OpenRoomOptions(BattleMode mode)
    {
        roomMode = mode;
        titleText.text = "进入游戏房间";
        firstText.text = "创建房间";
        secondText.text = "加入房间";
        firstBtn.onClick.RemoveAllListeners();
        secondBtn.onClick.RemoveAllListeners();
        firstBtn.onClick.AddListener(CreateRoom);
        secondBtn.onClick.AddListener(OpenJoinRoom);
    }

    /// <summary>打开退出游戏的选项，按钮直接执行退出或关闭逻辑。</summary>
    public void OpenQuitOptions()
    {
        titleText.text = "是否退出游戏？";
        firstText.text = "是";
        secondText.text = "否";
        firstBtn.onClick.RemoveAllListeners();
        secondBtn.onClick.RemoveAllListeners();
        firstBtn.onClick.AddListener(QuitGame);
        secondBtn.onClick.AddListener(Close);
    }

    /// <summary>确认创建房间，并关闭当前选项框。</summary>
    private void CreateRoom()
    {
        Close();
        RoomManager.Instance.CreateRoom(roomMode);
    }

    /// <summary>关闭当前选项框，显示大厅内的房间号输入区域。</summary>
    private void OpenJoinRoom()
    {
        Close();
        UIManager.Instance.GetOpenPanel<LobbyPanel>(PanelId.Lobby).OpenJoinRoom();
    }

    /// <summary>关闭选项框并退出应用。</summary>
    private void QuitGame()
    {
        Close();
        Application.Quit();
    }
}
