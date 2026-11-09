using UnityEditor;
using UnityEngine;

/// <summary>有动画时隐藏手填的三个阶段时间，避免同时维护两套时序。</summary>
[CustomEditor(typeof(SkillConfigSO))]
internal class SkillConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty clip = serializedObject.FindProperty("animationClip");
        SerializedProperty data = serializedObject.FindProperty("data");
        bool dodge = data.FindPropertyRelative("skillID").intValue == BattleConst.DodgeSkillID;
        if (!dodge)
        {
            EditorGUILayout.PropertyField(clip);
        }
        bool swordWave = data.FindPropertyRelative("effect").enumValueIndex == (int)SkillEffect.SwordWave;
        SerializedProperty end = data.GetEndProperty();
        SerializedProperty field = data.Copy();
        bool animationTiming = !dodge && (clip.objectReferenceValue != null ||
            data.FindPropertyRelative("skillID").intValue == BattleConst.BasicAttackSkillID);

        field.NextVisible(true);
        do
        {
            if (dodge && field.name != "skillID" && field.name != "skillType" &&
                field.name != "activeSeconds" && field.name != "moveDistance")
            {
                continue;
            }
            if ((!swordWave && (field.name == "projectileSpeed" || field.name == "projectileRadius")) ||
                (swordWave && (field.name == "angle" || field.name == "moveDistance")))
            {
                continue;
            }
            if (animationTiming && (field.name == "startupSeconds" ||
                field.name == "activeSeconds" || field.name == "recoverySeconds"))
            {
                continue;
            }
            EditorGUILayout.PropertyField(field, true);
        }
        while (field.NextVisible(false) && !SerializedProperty.EqualContents(field, end));

        if (dodge)
        {
            EditorGUILayout.HelpBox("右键冲刺：Active Seconds 是持续时间，Move Distance 是无阻挡时的位移距离。冲刺没有冷却，不需要 HitStart / HitEnd。", MessageType.Info);
        }
        else if (animationTiming)
        {
            EditorGUILayout.HelpBox(swordWave
                ? "HitStart 时发射剑气，HitEnd 后收招；时间从动画导出，射程、速度和半径在这里配置。"
                : "HitStart 到 HitEnd 期间按角色前方扇形判定。Range 是半径（米），Angle 是完整夹角（度）；时间从动画导出，冷却从起手计算。", MessageType.Info);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
