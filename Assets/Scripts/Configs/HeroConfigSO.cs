using UnityEngine;

/// <summary>
/// 英雄的编辑配置源。
/// Data 保存共享战斗数值，下面的资源引用只供客户端表现使用。
/// </summary>
[CreateAssetMenu(fileName = "HeroConfig", menuName = "秘核防线/Battle/Hero Config")]
public sealed class HeroConfigSO : ScriptableObject
{
    [Header("Battle Data")]
    [SerializeField] private HeroConfigData data = new HeroConfigData();

    [Header("Client View")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private RuntimeAnimatorController animator;
    [SerializeField] private Sprite icon;

    public HeroConfigData Data => data;
    public GameObject Prefab => prefab;
    public RuntimeAnimatorController Animator => animator;
    public Sprite Icon => icon;
}
