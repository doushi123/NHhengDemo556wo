using UnityEngine;

/// <summary>
/// 技能的编辑配置源。
/// Data 保存客户端和服务端共用的技能数值。
/// </summary>
[CreateAssetMenu(fileName = "SkillConfig", menuName = "秘核防线/Battle/Skill Config")]
public sealed class SkillConfigSO : ScriptableObject
{
    [Header("Battle Data")]
    [SerializeField] private SkillConfigData data = new SkillConfigData();

    [Tooltip("导出时读取 HitStart、HitEnd 和片段结尾，自动生成前摇、判定和后摇时间。")]
    [SerializeField] private AnimationClip animationClip;

    public SkillConfigData Data => data;
    public AnimationClip AnimationClip => animationClip;
}
