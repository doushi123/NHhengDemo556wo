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

        if (targetScene.IsValid() && targetScene.isLoaded)
        {
            SceneManager.SetActiveScene(targetScene);
        }

        SceneManager.UnloadSceneAsync(loadedScene);
    }
}
