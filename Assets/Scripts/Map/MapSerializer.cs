using System;
using System.IO;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#else
using System.Text.Json;
#endif

/// <summary>把JSON恢复成MapData</summary>
public static class MapSerializer
{
    public static MapData Deserialize(string json)
    {
#if UNITY_5_3_OR_NEWER
        MapData data = JsonUtility.FromJson<MapData>(json);
#else
        MapData? data = JsonSerializer.Deserialize<MapData>(json,
            new JsonSerializerOptions { IncludeFields = true });
#endif
        return data ?? throw new InvalidDataException("地图 JSON 内容为空。");
    }
}
