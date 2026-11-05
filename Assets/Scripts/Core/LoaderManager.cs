using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>全局加载管理器，负责异步切换场景、更新加载进度和关闭加载界面。</summary>
public class LoaderManager : AppService
{
    public static LoaderManager Instance { get; private set; }

    // 当前加载流程使用的界面；场景加载结束后，业务也可以保留它继续显示准备进度。
    private LoadingPanel panel;

    /// <summary>加载界面对应的流程是否仍在进行，包括切场景后的业务准备。</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Unity 是否仍在异步加载场景；关闭界面不会取消已经开始的场景操作。</summary>
    public bool IsSceneLoading { get; private set; }

    /// <summary>最近一次场景加载失败的原因，每次开始新加载时清空。</summary>
    public string Error { get; private set; }

    /// <summary>登记全局加载入口，加载界面在实际使用时创建。</summary>
    protected override void OnInitialize()
    {
        Instance = this;
    }

    /// <summary>
    /// 显示 Loading 并异步切换场景，默认加载完成后关闭界面。
    /// 还需业务初始化时，传入 false 保留界面，并用 sceneProgress 指定场景占总进度的比例。
    /// </summary>
    public void LoadScene(string sceneName, bool closeWhenLoaded = true, float sceneProgress = 1f)
    {
        if (IsLoading || IsSceneLoading)
        {
            throw new InvalidOperationException("上一次加载尚未结束，不能重复切换场景。");
        }

        panel = (LoadingPanel)UIManager.Instance.Open(PanelId.Loading);
        Error = null;
        IsLoading = true;
        IsSceneLoading = true;
        StartCoroutine(LoadSceneRoutine(sceneName, closeWhenLoaded, sceneProgress));
    }

    /// <summary>先显示一帧加载界面，再加载并激活场景，持续更新场景部分的进度。</summary>
    private IEnumerator LoadSceneRoutine(string sceneName, bool closeWhenLoaded, float sceneProgress)
    {
        yield return null;
        // 在真正发起切场景前取消流程时，不再开始加载。
        if (!IsLoading)
        {
            IsSceneLoading = false;
            yield break;
        }

        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                throw new InvalidOperationException(sceneName + " 未能开始加载，请检查 Build Settings。");
            }
        }
        catch (Exception exception)
        {
            Error = exception.Message;
            IsSceneLoading = false;
            CloseLoading();
            // 独立切场景由加载器提示；业务保留界面时，由调用方处理失败和提示。
            if (closeWhenLoaded)
            {
                Debug.LogError("场景加载失败：" + Error);
                UIManager.Instance.Open(PanelId.Tip, Error);
            }
        }
        if (operation == null)
        {
            yield break;
        }

        while (!operation.isDone)
        {
            if (IsLoading)
            {
                // Unity 的 0.9 表示场景加载完成待激活，再换算为本流程中的进度比例。
                SetProgress(Mathf.Clamp01(operation.progress / 0.9f) * sceneProgress, "晨辉与暮影的交界处，战火将起。");
            }
            yield return null;
        }
        IsSceneLoading = false;
        if (!IsLoading)
        {
            yield break;
        }

        SetProgress(sceneProgress, "秘核的光芒，正指引你前行。");
        if (closeWhenLoaded)
        {
            CloseLoading();
        }
    }

    /// <summary>更新加载界面的总进度和说明，供场景加载及后续业务准备共同使用。</summary>
    public void SetProgress(float progress, string status)
    {
        panel.SetProgress(progress, status);
    }

    /// <summary>结束当前加载流程并关闭界面；已经开始的 Unity 场景操作会继续完成。</summary>
    public void CloseLoading()
    {
        IsLoading = false;
        UIManager.Instance.Close(PanelId.Loading);
    }

    /// <summary>服务关闭时释放加载界面、协程和全局入口。</summary>
    protected override void OnShutdown()
    {
        CloseLoading();
        StopAllCoroutines();
        IsSceneLoading = false;
        Instance = null;
    }
}
