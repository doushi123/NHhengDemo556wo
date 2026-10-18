using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 显示网络和业务操作产生的短提示。
/// 面板保持激活，平时通过 CanvasGroup 隐藏并且不阻挡点击。
/// </summary>
public sealed class TipPanel : BasePanel
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float stayDuration = 2f;

    private Coroutine showCoroutine;

    private void Awake()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnDisable()
    {
        if (showCoroutine != null)
        {
            StopCoroutine(showCoroutine);
            showCoroutine = null;
        }

        canvasGroup.alpha = 0f;
    }

    public override void Open(object data = null)
    {
        base.Open(data);

        if (data is string content)
        {
            Show(content);
        }
    }

    public void Show(string content)
    {
        contentText.text = content;

        if (showCoroutine != null)
        {
            StopCoroutine(showCoroutine);
        }

        showCoroutine = StartCoroutine(PlayAnimation());
    }

    private IEnumerator PlayAnimation()
    {
        yield return Fade(canvasGroup.alpha, 1f);
        yield return new WaitForSecondsRealtime(stayDuration);
        yield return Fade(1f, 0f);
        showCoroutine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float time = 0f;
        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, time / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }
}
