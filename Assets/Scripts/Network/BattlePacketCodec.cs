using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text;

/// <summary>
/// 根据 BattlePacket 和 NetField 自动编解码 KCP 战斗消息。
/// </summary>
public static class BattlePacketCodec
{
    /// <summary>
    /// 战斗消息类型缓存容器。
    /// </summary>
    private static ConcurrentDictionary<Type, BattlePacketType> concurDic =
        new ConcurrentDictionary<Type, BattlePacketType>();

    /// <summary>
    /// 读取战斗包开头的消息类型。
    /// </summary>
    public static BattlePacketType GetPacketType(ArraySegment<byte> bytes)
    {
        using MemoryStream stream = new MemoryStream(bytes.Array, bytes.Offset, bytes.Count, false);
        using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8);
        return (BattlePacketType)reader.ReadInt32();
    }

    /// <summary>
    /// 编码任意标记了 BattlePacket 的战斗消息。
    /// </summary>
    public static byte[] Serialize<T>(T packet) where T : struct
    {
        using MemoryStream stream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write((int)GetPacketType(typeof(T)));
        BinaryDataSerializer.Write(writer, packet, typeof(T));
        return stream.ToArray();
    }

    /// <summary>
    /// 将战斗消息体恢复成指定的数据结构。
    /// 消息类型已经由外层 OnData 分发，这里只负责读取字段。
    /// </summary>
    public static T Deserialize<T>(ArraySegment<byte> bytes) where T : struct
    {
        using MemoryStream stream = new MemoryStream(bytes.Array, bytes.Offset, bytes.Count, false);
        using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8);
        reader.ReadInt32();
        return (T)BinaryDataSerializer.Read(reader, typeof(T));
    }

    /// <summary>
    /// 读取并缓存数据结构上声明的战斗消息类型。
    /// </summary>
    private static BattlePacketType GetPacketType(Type type)
    {
        return concurDic.GetOrAdd(type, currentType =>
        {
            BattlePacketAttribute attribute =currentType.GetCustomAttribute<BattlePacketAttribute>();
            if (attribute == null)
            {
                throw new InvalidOperationException(currentType.Name + " 没有标记 BattlePacket。");
            }
            return attribute.Type;
        });
    }
}
