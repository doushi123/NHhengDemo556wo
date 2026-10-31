using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text;

/// <summary>
/// TCP 消息的统一序列化器。
/// 它通过反射读取 NetMessage 和 NetField，然后自动完成消息与 byte[] 之间的转换。
/// </summary>
public static class NetSerializer
{
    /// <summary>
    /// 发送消息的容器。
    /// </summary>
    private static readonly ConcurrentDictionary<Type, int> messageIDCache =
        new ConcurrentDictionary<Type, int>();

    /// <summary>
    /// 接收消息的容器。
    /// </summary>
    private static readonly ConcurrentDictionary<int, Type> messageTypeCache =
        new ConcurrentDictionary<int, Type>();

    /// <summary>
    /// 扫描指定程序集中的所有网络消息，并提前建立缓存。
    /// 客户端在 NetManager 初始化时调用一次。
    /// </summary>
    public static void RegisterMessages(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            NetMessageAttribute attribute = type.GetCustomAttribute<NetMessageAttribute>();
            if (attribute == null || !typeof(BaseMsg).IsAssignableFrom(type))
            {
                continue;
            }
            
            messageIDCache[type] = attribute.ID;
            messageTypeCache[attribute.ID] = type;
        }
    }

    /// <summary>
    /// 把消息对象转换成完整字节数组。
    /// 完整格式为：4字节消息 ID + 4字节消息体长度 + 消息体。
    /// </summary>
    public static byte[] Serialize(BaseMsg msg)
    {
        // 先只序列化消息自己的字段，得到消息体。
        //先在内存中创建一个临时的"字节容器"，再把消息对象的所有字段写进去，最终得到消息体的byte[]
        byte[] bodyBytes;
        using (MemoryStream bodyStream = new MemoryStream())
        using (BinaryWriter bodyWriter = new BinaryWriter(bodyStream, Encoding.UTF8))
        {
            BinaryDataSerializer.Write(bodyWriter, msg, msg.GetType());
            bodyBytes = bodyStream.ToArray();
        }

        // 再在消息体前面补上公共消息头。
        using (MemoryStream messageStream = new MemoryStream())
        using (BinaryWriter messageWriter = new BinaryWriter(messageStream, Encoding.UTF8))
        {
            messageWriter.Write(GetMessageID(msg.GetType()));
            messageWriter.Write(bodyBytes.Length);
            messageWriter.Write(bodyBytes);
            return messageStream.ToArray();
        }
    }

    /// <summary>
    /// 根据消息 ID 创建对应消息对象，并从指定的消息体范围中恢复所有字段。
    /// 未注册的消息 ID 返回 null，由接收方忽略。
    /// </summary>
    public static BaseMsg Deserialize(int messageID, byte[] bytes, int beginIndex, int bodyLength)
    {
        if (!messageTypeCache.TryGetValue(messageID, out Type messageType))
        {
            return null;
        }

        BaseMsg msg;
        using (MemoryStream stream = new MemoryStream(bytes, beginIndex, bodyLength, false))
        using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
        {
            msg = (BaseMsg)BinaryDataSerializer.Read(reader, messageType);
        }

        return msg;
    }

    // 获取消息类型标记的 ID。未提前注册时，这里也会完成一次缓存。
    private static int GetMessageID(Type type)
    {
        return messageIDCache.GetOrAdd(type, currentType =>
        {
            NetMessageAttribute attribute = currentType.GetCustomAttribute<NetMessageAttribute>();
            if (attribute == null)
            {
                throw new InvalidOperationException(currentType.Name + " 没有标记 NetMessage。");
            }

            messageTypeCache[attribute.ID] = currentType;
            return attribute.ID;
        });
    }
}
