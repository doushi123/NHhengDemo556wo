using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>通用加载界面，显示场景加载及业务准备的进度与当前步骤。</summary>
public class LoadingPanel : BasePanel
{
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text statusText;

    /// <summary>打开时重置进度，背景图片由 Prefab 配置。</summary>
    public override void Open(object data = null)
    {
        base.Open(data);
        progressBar.minValue = 0f;
        progressBar.maxValue = 1f;
        progressBar.wholeNumbers = false;
        progressBar.interactable = false;
        SetProgress(0f, "循着秘核的微光，踏上新的征途。");
    }

    /// <summary>更新 0～1 范围的本机进度；此数值不代表其他玩家的加载情况。</summary>
    public void SetProgress(float progress, string status)
    {
        progress = Mathf.Clamp01(progress);
        progressBar.value = progress;
        progressText.text = Mathf.RoundToInt(progress * 100f) + "%";
        statusText.text = status;
    }
}
