using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#else
using System.Text.Json;
#endif

/// <summary>
/// 客户端与服务端共用的战斗配置 JSON 转换和版本 Hash 计算入口。
/// </summary>
public static class BattleConfigSerializer
{
#if !UNITY_5_3_OR_NEWER
    private static JsonSerializerOptions JsonOptions =
        new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
        };
#endif

    /// <summary>
    /// 将导出后的纯战斗配置转换成 JSON。
    /// </summary>
    public static string Serialize(BattleConfigData config)
    {
#if UNITY_5_3_OR_NEWER
        return JsonUtility.ToJson(config, true);
#else
        return JsonSerializer.Serialize(config, JsonOptions);
#endif
    }

    /// <summary>
    /// 从客户端与服务端共用的 JSON 恢复纯战斗配置。
    /// </summary>
    public static BattleConfigData Deserialize(string json)
    {
#if UNITY_5_3_OR_NEWER
        return JsonUtility.FromJson<BattleConfigData>(json);
#else
        return JsonSerializer.Deserialize<BattleConfigData>(json, JsonOptions)
            ?? throw new InvalidDataException("战斗配置 JSON 内容为空。");
#endif
    }

    /// <summary>
    /// 计算配置内容的 SHA256，用于进入战斗前核对双端配置是否一致。
    /// </summary>
    public static string GetHash(string json)
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        byte[] hashBytes;

        using (SHA256 sha256 = SHA256.Create())
        {
            hashBytes = sha256.ComputeHash(jsonBytes);
        }

        StringBuilder result = new StringBuilder(hashBytes.Length * 2);
        foreach (byte value in hashBytes)
        {
            result.Append(value.ToString("X2"));
        }
        return result.ToString();
    }
}
