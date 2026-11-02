using System;
using System.Collections.Generic;
using FixMath.NET;
using UnityEngine;

/// <summary>
/// 把Unity场景里的Collider整理成地图数据
/// </summary>
public static class MapBaker
{
    /// <summary>从场景提取碰撞数据。</summary>
    public static MapData Build(MapAuthoring authoring)
    {
        Bounds bounds = authoring.MapBounds;
        if (authoring.MapID <= 0 ||
            bounds.size.x <= 0f || bounds.size.y <= 0f || bounds.size.z <= 0f)
        {
            throw new InvalidOperationException("地图 ID 和边界尺寸必须大于 0。");
        }
        if (authoring.CollisionLayers.value == 0)
        {
            throw new InvalidOperationException("请在 Collision Layers 中选择地面和障碍物所在层。");
        }

        Physics.SyncTransforms();
        List<Collider> colliders = CollectColliders(authoring, bounds);
        if (colliders.Count == 0)
        {
            throw new InvalidOperationException("所选层和边界内没有可导出的静态 Collider，请检查层、范围及组件是否启用。");
        }

        List<MapBox> boxes = new List<MapBox>();
        List<MapSphere> spheres = new List<MapSphere>();
        List<MapCapsule> capsules = new List<MapCapsule>();
        List<MapMesh> meshes = new List<MapMesh>();
        foreach (Collider collider in colliders)
        {
            if (collider is BoxCollider box)
            {
                // 按 Unity 盒体的中心、世界旋转和缩放导出，不要求模型变换矩阵的三个轴正交。
                boxes.Add(new MapBox
                {
                    center = ToVector(box.transform.TransformPoint(box.center)),
                    size = ToVector(Vector3.Scale(box.size, AbsScale(box.transform))),
                    rotation = ToRotation(box.transform.rotation),
                });
            }
            else if (collider is SphereCollider sphere)
            {
                Vector3 scale = AbsScale(sphere.transform);
                spheres.Add(new MapSphere
                {
                    center = ToVector(sphere.transform.TransformPoint(sphere.center)),
                    radius = (Fix64)(sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z))),
                });
            }
            else if (collider is CapsuleCollider capsule)
            {
                capsules.Add(BuildCapsule(capsule));
            }
            else if (collider is MeshCollider mesh)
            {
                meshes.Add(BuildMesh(mesh));
            }
            else
            {
                throw new InvalidOperationException(collider.name + "：暂不支持导出 " +
                    collider.GetType().Name + "，请使用 Box、Sphere、Capsule 或非 Convex MeshCollider。");
            }
        }

        return new MapData
        {
            mapID = authoring.MapID,
            boundsMin = ToVector(bounds.min),
            boundsMax = ToVector(bounds.max),
            boxes = boxes.ToArray(),
            spheres = spheres.ToArray(),
            capsules = capsules.ToArray(),
            meshes = meshes.ToArray(),
        };
    }

    /// <summary>按场景层级顺序收集所选层与边界相交的静态碰撞体。</summary>
    private static List<Collider> CollectColliders(MapAuthoring authoring, Bounds bounds)
    {
        List<Collider> result = new List<Collider>();
        foreach (GameObject root in authoring.gameObject.scene.GetRootGameObjects())
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger ||
                    collider.attachedRigidbody != null || collider is CharacterController ||
                    (authoring.CollisionLayers.value & (1 << collider.gameObject.layer)) == 0 ||
                    collider.GetComponentInParent<PlayerController>() != null ||
                    collider.GetComponentInParent<Canvas>() != null)
                {
                    continue;
                }
                if (bounds.Intersects(collider.bounds))
                {
                    result.Add(collider);
                }
            }
        }
        return result;
    }

    /// <summary>按 Unity 胶囊的轴向与缩放规则计算世界球心、半径。</summary>
    private static MapCapsule BuildCapsule(CapsuleCollider capsule)
    {
        Vector3 scale = AbsScale(capsule.transform);
        int axis = capsule.direction;
        float radiusScale = Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]);
        float radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * scale[axis], radius * 2f);
        Vector3 direction = Vector3.zero;
        direction[axis] = 1f;
        direction = capsule.transform.rotation * direction;
        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        Vector3 offset = direction * (height * 0.5f - radius);
        return new MapCapsule
        {
            pointA = ToVector(center - offset),
            pointB = ToVector(center + offset),
            radius = (Fix64)radius,
        };
    }

    /// <summary>导出碰撞网格，应用世界变换，并修正镜像缩放造成的三角形反向。</summary>
    private static MapMesh BuildMesh(MeshCollider collider)
    {
        if (collider.convex || collider.sharedMesh == null)
        {
            throw new InvalidOperationException(collider.name +"：MeshCollider 必须绑定网格并关闭 Convex，才能按实际三角网格导出。");
        }

        Vector3[] vertices;
        int[] triangles;
        try
        {
            vertices = collider.sharedMesh.vertices;
            triangles = collider.sharedMesh.triangles;
        }
        catch (UnityException exception)
        {
            throw new InvalidOperationException(collider.name +
                "：无法读取碰撞网格，请检查模型的 Read/Write 设置。" + exception.Message, exception);
        }
        if (vertices.Length == 0 || triangles.Length == 0)
        {
            throw new InvalidOperationException(collider.name + "：碰撞网格没有顶点或三角形。");
        }

        MapVector[] points = new MapVector[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            points[i] = ToVector(collider.transform.TransformPoint(vertices[i]));
        }
        if (collider.transform.localToWorldMatrix.determinant < 0f)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int index = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = index;
            }
        }
        return new MapMesh { vertices = points, triangles = triangles };
    }

    /// <summary>取得缩放绝对值。</summary>
    private static Vector3 AbsScale(Transform transform)
    {
        Vector3 scale = transform.lossyScale;
        return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    /// <summary>在烘焙边界把 Unity 浮点坐标转换为定点坐标，加载后直接用于模拟。</summary>
    private static MapVector ToVector(Vector3 value)
    {
        return new MapVector
        {
            x = (Fix64)value.x,
            y = (Fix64)value.y,
            z = (Fix64)value.z,
        };
    }

    /// <summary>把 Unity 四元数转换成物理库可还原的定点四元数分量。</summary>
    private static MapRotation ToRotation(Quaternion value)
    {
        return new MapRotation
        {
            x = (Fix64)value.x,
            y = (Fix64)value.y,
            z = (Fix64)value.z,
            w = (Fix64)value.w,
        };
    }
}
