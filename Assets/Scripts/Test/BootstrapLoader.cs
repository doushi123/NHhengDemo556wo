using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 保证游戏无论从哪个场景启动，都先完成 BootStrap 场景里的全局初始化。
/// </summary>
public static class BootstrapLoader
{
    private const string BootstrapSceneName = "BootStrap";

    /// <summary>
    /// 记录启动时的当前场景，确保加载 Bootstrap 后仍回到它。
    /// </summary>
    private static Scene targetScene;

    /// <summary>
    /// 防止 Bootstrap 尚未加载完成时重复发起加载。
    /// </summary>
    private static bool loadingBootstrap;

    // 只记录本次启动时暂停的角色，管理器就绪后恢复；不改变原本禁用的角色。
    private static List<PlayerController> pausedPlayers = new List<PlayerController>();

    /// <summary>
    /// 首个场景加载完成后检查全局初始化状态。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureBootstrap()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        if (!currentScene.IsValid())
        {
            return;
        }

        // 直接从 BootStrap 启动时不需要再次加载它。
        if (currentScene.name == BootstrapSceneName)
        {
            return;
        }

        // 已经有全局 AppRoot，说明 Bootstrap 已经处理过当前进程。
        if (AppRoot.Instance != null || loadingBootstrap)
        {
            return;
        }

        targetScene = currentScene;
        loadingBootstrap = true;
        foreach (PlayerController player in Object.FindObjectsOfType<PlayerController>())
        {
            if (player.enabled)
            {
                pausedPlayers.Add(player);
                player.enabled = false;
            }
        }
        SceneManager.sceneLoaded += OnBootstrapLoaded;
        SceneManager.LoadSceneAsync(BootstrapSceneName, LoadSceneMode.Additive);
    }

    /// <summary>
    /// Bootstrap 加载完成后，AppRoot 已经执行 Awake；保留全局管理器并卸载临时场景。
    /// </summary>
    private static void OnBootstrapLoaded(Scene loadedScene, LoadSceneMode mode)
    {
        if (loadedScene.name != BootstrapSceneName)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnBootstrapLoaded;
        loadingBootstrap = false;

        if (AppRoot.Instance == null)
        {
            Debug.LogError("Bootstrap 场景没有初始化 AppRoot，无法完成全局启动。");
            return;
        }

        // 启动配置缺少角色必需的服务时在入口明确报错，不让业务每帧静默跳过。
        if (BattleNetManager.Instance == null || !BattleNetManager.Instance.IsInitialized)
        {
            Debug.LogError("Bootstrap 未初始化 BattleNetManager，请检查 AppRoot 的服务列表。角色保持暂停。");
            return;
        }

        if (targetScene.IsValid() && targetScene.isLoaded)
        {
            SceneManager.SetActiveScene(targetScene);
        }

        foreach (PlayerController player in pausedPlayers)
        {
            // 加载期间角色可能随场景被销毁，只恢复仍存在的对象。
            if (player != null)
            {
                player.enabled = true;
            }
        }
        pausedPlayers.Clear();

        SceneManager.UnloadSceneAsync(loadedScene);
    }
}
