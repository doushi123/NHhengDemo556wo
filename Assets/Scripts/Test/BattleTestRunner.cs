using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// 自动进入战斗的客户端测试入口。
/// 只把 BattleTestMode.Enabled 改成 true 才会创建，false 时完全不参与正式流程。
/// </summary>
public sealed class BattleTestRunner : MonoBehaviour
{
    private static BattleTestRunner instance;
    /// <summary>正式导出地图去掉缩进后的上传文本，仅供本测试入口发送及开局校验。</summary>
    private string mapJson;

    /// <summary>
    /// 场景加载后创建测试入口；关闭测试模式时不创建任何对象。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRunner()
    {
        if (!BattleTestMode.Enabled || instance != null ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "BootStrap")
        {
            return;
        }

        GameObject runnerObject = new GameObject("BattleTestRunner");
        DontDestroyOnLoad(runnerObject);
        instance = runnerObject.AddComponent<BattleTestRunner>();
    }

    private void Start()
    {
        StartCoroutine(ConnectTestBattle());
    }

    /// <summary>
    /// 等待 Bootstrap 初始化网络管理器，连接 TCP 后请求服务端创建测试战斗。
    /// </summary>
    private IEnumerator ConnectTestBattle()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        PlayerController player = null;
        MapAuthoring map = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            player = FindObjectOfType<PlayerController>();
            map = FindObjectOfType<MapAuthoring>();
            if (NetManager.Instance != null && BattleNetManager.Instance != null &&
                player != null && map != null)
            {
                break;
            }
            yield return null;
        }

        if (NetManager.Instance == null || BattleNetManager.Instance == null ||
            player == null || map == null)
        {
            ShowError("测试模式需要 BootStrap 管理器、PlayerController 和 MapAuthoring。");
            yield break;
        }

        try
        {
            // 运行时读取编辑器已烘焙的地图，不能重新访问可能未开启 Read/Write 的模型网格。
            string mapPath = Path.Combine(Application.streamingAssetsPath,
                "Maps", "Map_" + map.MapID + ".json");
            MapData mapData = MapSerializer.Deserialize(File.ReadAllText(mapPath));
            // 测试入口通过 TCP 上传地图，省去排版空白，避免三维网格被缩进撑过单包上限。
            mapJson = JsonUtility.ToJson(mapData);
            Debug.Log("测试地图已读取：Map_" + mapData.mapID +
                "，三维碰撞体 " + (mapData.boxes.Length + mapData.spheres.Length +
                mapData.capsules.Length + mapData.meshes.Length) + " 个。");
        }
        catch (System.Exception exception)
        {
            ShowError("测试地图读取失败：" + exception.Message +
                "\n请在编辑器中选中 MapAuthoring，点击“导出地图数据”后重新运行。");
            yield break;
        }

        UIManager.Instance.Close(PanelId.Login);
        NetManager.Instance.Connect();
        while (!NetManager.Instance.IsConnected && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (!NetManager.Instance.IsConnected)
        {
            ShowError("测试模式无法连接服务端。");
            yield break;
        }

        NetManager.Instance.Send(new BattleTestRequestMsg
        {
            mapID = map.MapID,
            mapJson = mapJson,
        });
        Debug.Log("测试战斗请求已发送，等待服务端 Token。");
    }

    /// <summary>
    /// 收到服务端创建的测试 Token 后，仍通过正式 BattleNetManager 连接 KCP。
    /// </summary>
    public static void ReceiveToken(BattleTestTokenMsg message)
    {
        if (BattleTestMode.Enabled && instance != null)
        {
            if (string.IsNullOrEmpty(message.battleToken))
            {
                instance.ShowError("测试战斗创建失败：" + message.error);
                return;
            }

            BattleNetManager.Instance.Connect(message.battleToken);
            BattleNetManager.Instance.SetUploadedMapForTest(instance.mapJson);
            Debug.Log("测试 Token 已收到，正在进行 KCP 战斗认证。");
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void ShowError(string message)
    {
        Debug.LogWarning(message);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.Open(PanelId.Tip, message);
        }
    }
}
