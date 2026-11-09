using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>在地图配置组件上提供三维碰撞预览与双端共用 JSON 导出。</summary>
[CustomEditor(typeof(MapAuthoring))]
internal class MapExporter : Editor
{
    private MapData preview;
    // 缓存从定点顶点还原的三角形边，避免 Scene 每次重绘都重新遍历和分配顶点。
    private Vector3[] meshLines = Array.Empty<Vector3>();

    /// <summary>显示层选择、预览/导出按钮、形状统计和可定位的烘焙错误。</summary>
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            preview = null;
            meshLines = Array.Empty<Vector3>();
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "Collision Layers 选择地面和静态障碍所在层，例如 BattleGround 与 BattleBlocker。" +
            "只收集当前场景、边界内的碰撞体；不需要地图父节点。", MessageType.Info);

        MapAuthoring authoring = (MapAuthoring)target;
        if (GUILayout.Button("预览碰撞数据"))
        {
            BuildPreview(authoring);
        }
        if (GUILayout.Button("导出地图数据"))
        {
            BuildPreview(authoring);
            Export(preview);
        }
        if (preview != null)
        {
            EditorGUILayout.LabelField("盒体", preview.boxes.Length.ToString());
            EditorGUILayout.LabelField("球体", preview.spheres.Length.ToString());
            EditorGUILayout.LabelField("胶囊体", preview.capsules.Length.ToString());
            EditorGUILayout.LabelField("三角网格", preview.meshes.Length.ToString());
            EditorGUILayout.LabelField("三角形", (meshLines.Length / 6).ToString());
            EditorGUILayout.LabelField("秘核基地", preview.cores.Length.ToString());
            if (preview.monsters != null)
            {
                EditorGUILayout.LabelField("怪物出生区域", preview.monsters.spawns.Length.ToString());
                EditorGUILayout.LabelField("每波总预算", preview.monsters.waveBudget.ToString());
                EditorGUILayout.LabelField("每方强度上限 / 全场数量上限", preview.monsters.maxCampStrength + " / " + preview.monsters.maxAlive);
            }
            EditorGUILayout.HelpBox("线框显示上次烘焙的实际数据。修改 Collider 后请重新预览或导出。" +
                "角色移动由 BEPU 定点物理计算，地面和障碍都必须在所选层中。", MessageType.Info);
        }
    }

    /// <summary>重新烘焙并缓存网格线；失败时由 Unity 直接报告原始错误。</summary>
    private void BuildPreview(MapAuthoring authoring)
    {
        preview = null;
        meshLines = Array.Empty<Vector3>();
        preview = MapBaker.Build(authoring);
        List<Vector3> lines = new List<Vector3>();
        foreach (MapMesh mesh in preview.meshes)
        {
            for (int i = 0; i < mesh.triangles.Length; i += 3)
            {
                Vector3 a = ToVector(mesh.vertices[mesh.triangles[i]]);
                Vector3 b = ToVector(mesh.vertices[mesh.triangles[i + 1]]);
                Vector3 c = ToVector(mesh.vertices[mesh.triangles[i + 2]]);
                lines.Add(a);
                lines.Add(b);
                lines.Add(b);
                lines.Add(c);
                lines.Add(c);
                lines.Add(a);
            }
        }
        meshLines = lines.ToArray();
        SceneView.RepaintAll();
    }

    /// <summary>直接绘制烘焙数据：绿色为地形网格，黄色为简单形状，青色为地图范围。</summary>
    private void OnSceneGUI()
    {
        if (preview == null)
        {
            return;
        }

        CompareFunction oldZTest = Handles.zTest;
        Color oldColor = Handles.color;
        Handles.zTest = CompareFunction.Always;
        try
        {
            Handles.color = Color.cyan;
            Vector3 min = ToVector(preview.boundsMin);
            Vector3 max = ToVector(preview.boundsMax);
            Handles.DrawWireCube((min + max) * 0.5f, max - min);

            Handles.color = Color.green;
            if (meshLines.Length > 0)
            {
                Handles.DrawLines(meshLines);
            }
            Handles.color = Color.yellow;
            foreach (MapBox box in preview.boxes)
            {
                Quaternion rotation = new Quaternion(
                    (float)box.rotation.x, (float)box.rotation.y,
                    (float)box.rotation.z, (float)box.rotation.w);
                using (new Handles.DrawingScope(Matrix4x4.TRS(ToVector(box.center), rotation, Vector3.one)))
                {
                    Handles.DrawWireCube(Vector3.zero, ToVector(box.size));
                }
            }
            foreach (MapSphere sphere in preview.spheres)
            {
                DrawSphere(ToVector(sphere.center), (float)sphere.radius);
            }
            foreach (MapCapsule capsule in preview.capsules)
            {
                DrawCapsule(capsule);
            }
            foreach (MapCoreData core in preview.cores)
            {
                Handles.color = core.camp == BattleCamp.CampA ? Color.blue : Color.red;
                DrawSphere(ToVector(core.center), (float)core.radius);
            }
        }
        finally
        {
            Handles.zTest = oldZTest;
            Handles.color = oldColor;
        }
    }

    /// <summary>用三个互相垂直的大圆显示球体边界。</summary>
    private static void DrawSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }

    /// <summary>根据两端球心绘制胶囊的圆柱侧面和外侧半球，支持任意轴向。</summary>
    private static void DrawCapsule(MapCapsule capsule)
    {
        Vector3 a = ToVector(capsule.pointA);
        Vector3 b = ToVector(capsule.pointB);
        float radius = (float)capsule.radius;
        if ((b - a).sqrMagnitude < 0.000001f)
        {
            DrawSphere(a, radius);
            return;
        }

        Vector3 axis = (b - a).normalized;
        Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        Vector3 side = Vector3.Cross(axis, reference).normalized;
        Vector3 front = Vector3.Cross(side, axis);
        Handles.DrawWireDisc(a, axis, radius);
        Handles.DrawWireDisc(b, axis, radius);
        Handles.DrawLine(a + side * radius, b + side * radius);
        Handles.DrawLine(a - side * radius, b - side * radius);
        Handles.DrawLine(a + front * radius, b + front * radius);
        Handles.DrawLine(a - front * radius, b - front * radius);
        Handles.DrawWireArc(a, side, front, 180f, radius);
        Handles.DrawWireArc(b, -side, front, 180f, radius);
        Handles.DrawWireArc(a, -front, side, 180f, radius);
        Handles.DrawWireArc(b, front, side, 180f, radius);
    }

    /// <summary>只为编辑器预览把定点坐标转换为 Unity 浮点坐标。</summary>
    private static Vector3 ToVector(MapVector value)
    {
        return new Vector3((float)value.x, (float)value.y, (float)value.z);
    }

    /// <summary>写入客户端与服务端构建共同使用的地图 JSON，沿用当前文件路径。</summary>
    private static void Export(MapData data)
    {
        string directory = "Assets/StreamingAssets/Maps";
        string path = Path.Combine(directory, "Map_" + data.mapID + ".json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log("地图已导出：" + path + "，三维碰撞体：" +
            (data.boxes.Length + data.spheres.Length + data.capsules.Length + data.meshes.Length));
    }
}
