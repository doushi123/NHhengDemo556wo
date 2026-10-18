using UnityEngine;

/// <summary>
/// 负责客户端启动完成后的初始流程。
/// 需要放在 AppRoot 的服务列表最后初始化。
/// </summary>
public sealed class GameStartManager : AppService
{
    [SerializeField] private string loginMusicID;

    protected override void OnInitialize()
    {
        UIManager.Instance.Open(PanelId.Login);

        if (!string.IsNullOrEmpty(loginMusicID))
        {
            AudioManager.Instance.PlayMusic(loginMusicID);
        }
    }
}
