using FixMath.NET;
using UnityEngine;

/// <summary>秘核基地，按模拟结果显示阵营标记、生命和受击反馈。</summary>
public class CoreBase : MonoBehaviour
{
    [SerializeField] private BattleCamp camp = BattleCamp.CampA;
    /// <summary>
    /// 营地初始生命
    /// </summary>
    [SerializeField] private int maxHp = 2000;
    /// <summary>导出基地受击范围的球形碰撞体，在 Inspector 中指定。</summary>
    [SerializeField] private SphereCollider body;
    /// <summary>
    /// 基地的模型对象
    /// </summary>
    [SerializeField] private GameObject model;
    /// <summary>
    /// 基地阵营标记的渲染组件
    /// </summary>
    [SerializeField] private MeshRenderer marker;
    /// <summary>
    /// 基地能量罩的渲染组件
    /// </summary>
    [SerializeField] private MeshRenderer shield;
    /// <summary>
    /// 一次受击反馈多少秒
    /// </summary>
    [SerializeField] private float hitEffectSeconds = 0.45f;
    [SerializeField] private Color ownColor = new Color(0.1f, 0.85f, 1f, 1f);
    [SerializeField] private Color enemyColor = new Color(1f, 0.2f, 0.12f, 1f);
    private BattleSimulation simulation;
    /// <summary>
    /// 准备传给Renderer的渲染参数
    /// </summary>
    private MaterialPropertyBlock markerProperties;
    /// <summary>
    /// 记录已经播放过反馈的一次受击逻辑帧
    /// </summary>
    private int displayedHitFrame = -1;
    private float flashUntil = -1f;

    private static int colorID = Shader.PropertyToID("_BaseColor");
    private static int healthID = Shader.PropertyToID("_Health");
    private static int flashID = Shader.PropertyToID("_HitFlash");

    /// <summary>连续本局模拟并重置外观</summary>
    public void PrepareBattle(BattleSimulation battle)
    {
        simulation = battle;
        model.SetActive(true);
        displayedHitFrame = -1;
        flashUntil = -1f;
        if (marker != null || shield != null)
        {
            markerProperties = new MaterialPropertyBlock();
            BattleCamp ownCamp = battle.GetPlayerState(BattleNetManager.Instance.SeatIndex).camp;
            markerProperties.SetColor(colorID, camp == ownCamp ? ownColor : enemyColor);
            markerProperties.SetFloat(healthID, 1f);
            markerProperties.SetFloat(flashID, 0f);
            ApplyEnergyProperties(true);
        }
    }


    /// <summary>把场景中的基地导出成模拟配置。</summary>
    public MapCoreData BuildData()
    {
        //取得世界坐标点和缩放
        Vector3 center = body.transform.TransformPoint(body.center);
        Vector3 scale = body.transform.lossyScale;

        float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        return new MapCoreData
        {
            camp = camp,
            center = new MapVector { x = (Fix64)center.x, y = (Fix64)center.y, z = (Fix64)center.z },
            radius = (Fix64)(body.radius * radiusScale),
            maxHp = maxHp, 
        };
    }

    private void Update()
    {
        // 场景刚加载时还没有本局模拟，先保留编辑器中摆好的外观。
        if (simulation == null)
        {
            return;
        }
        CoreBaseState state = simulation.GetCoreState(camp);
        bool visible = !state.IsDestroyed;
        if (marker != null || shield != null)
        {
            if (state.lastHitFrame >= 0 && state.lastHitFrame != displayedHitFrame)
            {
                displayedHitFrame = state.lastHitFrame;
                flashUntil = Time.unscaledTime + hitEffectSeconds;
            }
            markerProperties.SetFloat(healthID, (float)state.hp / state.maxHp);
            markerProperties.SetFloat(flashID, Mathf.Clamp01((flashUntil - Time.unscaledTime) / hitEffectSeconds));
            ApplyEnergyProperties(visible);
        }
        if (model.activeSelf != visible)
        {
            model.SetActive(visible);
        }
    }

    private void ApplyEnergyProperties(bool visible)
    {
        if (marker != null)
        {
            marker.enabled = visible;
            marker.SetPropertyBlock(markerProperties);
        }
        if (shield != null)
        {
            shield.enabled = visible;
            shield.SetPropertyBlock(markerProperties);
        }
    }
}
