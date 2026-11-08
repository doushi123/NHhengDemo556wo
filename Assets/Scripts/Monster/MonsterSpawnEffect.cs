using UnityEngine;

/// <summary>固定在怪物出生地点的一次性魔能表现；进度只影响材质，不参与模拟。</summary>
public class MonsterSpawnEffect : MonoBehaviour
{
    [SerializeField] private MeshRenderer[] surfaces;
    [SerializeField, Min(0.1f)] private float duration = 0.9f;
    private MaterialPropertyBlock properties;
    private float startedAt;
    private static readonly int progressID = Shader.PropertyToID("_Progress");

    public void Play(Vector3 position)
    {
        transform.position = position;
        startedAt = Time.unscaledTime;
        SetProgress(0f);
        gameObject.SetActive(true);
    }

    private void Update()
    {
        float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
        SetProgress(progress);
        if (progress >= 1f) gameObject.SetActive(false);
    }

    private void SetProgress(float progress)
    {
        if (properties == null) properties = new MaterialPropertyBlock();
        properties.SetFloat(progressID, progress);
        foreach (MeshRenderer surface in surfaces) surface.SetPropertyBlock(properties);
    }
}
