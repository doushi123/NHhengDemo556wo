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
    private static void Export()
    {
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
    /// 提取 ScriptableObject 中的共享战斗数据，写入双端共用的配置文件。
    /// </summary>
    private static SkillConfigData CreateSkillConfig(SkillConfigSO source)
    {
        return source.Data;
    }
}
