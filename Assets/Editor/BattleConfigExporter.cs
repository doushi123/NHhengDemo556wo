using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 Unity 中的英雄和技能 ScriptableObject 转换成客户端与服务端共用的战斗配置 JSON。
/// </summary>
internal static class BattleConfigExporter
{
    private const string OutputDirectory = "Assets/StreamingAssets";

    [MenuItem("Tools/Battle/Export Config")]
    internal static void Export()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请退出 Play Mode 后导出战斗配置。");
        }
        HeroConfigSO[] heroAssets = FindAssets<HeroConfigSO>()
            .OrderBy(config => config.Data.heroID)
            .ToArray();
        SkillConfigSO[] skillAssets = FindAssets<SkillConfigSO>()
            .OrderBy(config => config.Data.skillID)
            .ToArray();

        CheckConfigs(heroAssets, skillAssets);

        BattleConfigData config = new BattleConfigData
        {
            heroes = heroAssets.Select(CreateHeroConfig).ToArray(),
            skills = skillAssets.Select(CreateSkillConfig).ToArray(),
        };

        string json = BattleConfigSerializer.Serialize(config);
        string outputPath = Path.Combine(OutputDirectory, BattleConst.ConfigFileName);

        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(outputPath, json, new UTF8Encoding(false));
        AssetDatabase.Refresh();

        Debug.Log("战斗配置已导出：" + outputPath + "\n配置 Hash：" + BattleConfigSerializer.GetHash(json));
    }

    /// <summary>
    /// 查找项目中某种类型的全部 ScriptableObject 配置资产。
    /// </summary>
    private static IEnumerable<T> FindAssets<T>() where T : ScriptableObject
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            yield return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }

    /// <summary>
    /// 在导出阶段暴露重复配置，避免生成一份无法确定读取结果的 JSON。
    /// </summary>
    private static void CheckConfigs(
        HeroConfigSO[] heroes,
        SkillConfigSO[] skills)
    {
        if (heroes.Select(config => config.Data.heroID).Distinct().Count() != heroes.Length)
        {
            throw new InvalidOperationException("存在重复的 heroID。");
        }

        if (skills.Select(config => config.Data.skillID).Distinct().Count() != skills.Length)
        {
            throw new InvalidOperationException("存在重复的 skillID。");
        }
    }

    /// <summary>
    /// 提取英雄参与确定性模拟的数值。
    /// </summary>
    private static HeroConfigData CreateHeroConfig(HeroConfigSO source)
    {
        return source.Data;
    }

    /// <summary>
    /// 导出技能数值；绑定动画后，三个阶段的时间以动画标记为准，不再手填。
    /// </summary>
    internal static SkillConfigData CreateSkillConfig(SkillConfigSO source)
    {
        // 只修改导出副本，不把生成的时间写回用户维护的 SO。
        SkillConfigData data = JsonUtility.FromJson<SkillConfigData>(JsonUtility.ToJson(source.Data));
        AnimationClip clip = source.AnimationClip;
        if (data.skillID == BattleConst.DodgeSkillID)
        {
            // 冲刺不需要攻击命中事件，也没有冷却；持续时间和距离由冲刺配置决定。
            data.cooldownSeconds = 0f;
            return data;
        }
        if (clip == null)
        {
            if (data.skillID == BattleConst.BasicAttackSkillID || data.skillType == SkillType.Attack)
            {
                throw new InvalidOperationException(source.name + "：攻击技能需要绑定动画片段。");
            }
            return data;
        }

        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        AnimationEvent[] starts = events.Where(item => item.functionName == "HitStart").ToArray();
        AnimationEvent[] ends = events.Where(item => item.functionName == "HitEnd").ToArray();
        if (starts.Length != 1 || ends.Length != 1)
        {
            throw new InvalidOperationException(clip.name + "：需要各一个 HitStart 和 HitEnd 事件。");
        }

        // AnimationUtility 返回秒数；FBX 导入设置里的归一化时间不能直接当秒使用。
        float hitStart = starts[0].time;
        float hitEnd = ends[0].time;
        if (clip.isLooping)
        {
            throw new InvalidOperationException(clip.name + "：攻击动画不能循环。");
        }

        data.startupSeconds = hitStart;
        data.activeSeconds = hitEnd - hitStart;
        data.recoverySeconds = clip.length - hitEnd;
        return data;
    }
}
