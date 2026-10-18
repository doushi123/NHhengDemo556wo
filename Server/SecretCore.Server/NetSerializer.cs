using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

/// <summary>
/// TCP 消息的统一序列化器。
/// 它通过反射读取 NetMessage 和 NetField，然后自动完成消息与 byte[] 之间的转换。
/// </summary>
public static class NetSerializer
{
    // 某个类型需要传输哪些字段。第一次反射后保存起来，之后不再重复扫描。
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> fieldCache =
        new ConcurrentDictionary<Type, FieldInfo[]>();

    // 消息类型 -> 消息 ID，发送消息时使用。
    private static readonly ConcurrentDictionary<Type, int> messageIDCache =
        new ConcurrentDictionary<Type, int>();

    // 消息 ID -> 消息类型，收到消息时使用。
    private static readonly ConcurrentDictionary<int, Type> messageTypeCache =
        new ConcurrentDictionary<int, Type>();

    /// <summary>
    /// 扫描指定程序集中的所有网络消息，并提前建立缓存。
    /// 服务端在 ServerSocket 启动时调用一次。
    /// </summary>
    public static void RegisterMessages(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            NetMessageAttribute? attribute = type.GetCustomAttribute<NetMessageAttribute>();
            if (attribute == null || !typeof(BaseMsg).IsAssignableFrom(type))
            {
                continue;
            }

            messageIDCache[type] = attribute.ID;
            messageTypeCache[attribute.ID] = type;
            GetFields(type);
        }
    }

    /// <summary>
    /// 把消息对象转换成可直接交给 Socket.Send 的完整字节数组。
    /// 完整格式为：4 字节消息 ID + 4 字节消息体长度 + 消息体。
    /// </summary>
    public static byte[] Serialize(BaseMsg msg)
    {
        // 先只序列化消息自己的字段，得到消息体。
        byte[] bodyBytes;
        using (MemoryStream bodyStream = new MemoryStream())
        using (BinaryWriter bodyWriter = new BinaryWriter(bodyStream, Encoding.UTF8))
        {
            WriteObject(bodyWriter, msg, msg.GetType());
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
    public static BaseMsg? Deserialize(int messageID, byte[] bytes, int beginIndex, int bodyLength)
    {
        if (!messageTypeCache.TryGetValue(messageID, out Type? messageType))
        {
            return null;
        }

        // 消息类需要有无参构造函数，反序列化器先创建空对象，再依次给字段赋值。
        BaseMsg msg = (BaseMsg)Activator.CreateInstance(messageType)!;
        using (MemoryStream stream = new MemoryStream(bytes, beginIndex, bodyLength, false))
        using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
        {
            ReadObject(reader, msg, messageType);
        }

        return msg;
    }

    // 获取消息类型标记的 ID。未提前注册时，这里也会完成一次缓存。
    private static int GetMessageID(Type type)
    {
        return messageIDCache.GetOrAdd(type, currentType =>
        {
            NetMessageAttribute? attribute = currentType.GetCustomAttribute<NetMessageAttribute>();
            if (attribute == null)
            {
                throw new InvalidOperationException(currentType.Name + " 没有标记 NetMessage。");
            }

            messageTypeCache[attribute.ID] = currentType;
            return attribute.ID;
        });
    }

    // 只收集标记了 NetField 的字段，并按 Order 从小到大排列。
    private static FieldInfo[] GetFields(Type type)
    {
        return fieldCache.GetOrAdd(type, currentType => currentType
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => new
            {
                Field = field,
                Attribute = field.GetCustomAttribute<NetFieldAttribute>(),
            })
            .Where(item => item.Attribute != null)
            .OrderBy(item => item.Attribute!.Order)
            .Select(item => item.Field)
            .ToArray());
    }

    // 按缓存中的字段顺序，逐个取值并写入。
    private static void WriteObject(BinaryWriter writer, object value, Type type)
    {
        foreach (FieldInfo field in GetFields(type))
        {
            WriteValue(writer, field.GetValue(value), field.FieldType);
        }
    }

    // 按相同顺序逐个读取，并通过反射把值赋给字段。
    private static void ReadObject(BinaryReader reader, object value, Type type)
    {
        foreach (FieldInfo field in GetFields(type))
        {
            field.SetValue(value, ReadValue(reader, field.FieldType));
        }
    }

    // 根据字段的真实类型，选择对应的写入方式。
    private static void WriteValue(BinaryWriter writer, object? value, Type type)
    {
        if (type == typeof(int))
        {
            writer.Write((int)value!);
        }
        else if (type == typeof(long))
        {
            writer.Write((long)value!);
        }
        else if (type == typeof(float))
        {
            writer.Write((float)value!);
        }
        else if (type == typeof(bool))
        {
            writer.Write((bool)value!);
        }
        else if (type == typeof(string))
        {
            WriteString(writer, (string?)value);
        }
        else if (type.IsEnum)
        {
            writer.Write(Convert.ToInt32(value));
        }
        else if (type.IsArray)
        {
            WriteArray(writer, (Array?)value, type.GetElementType()!);
        }
        else
        {
            // 不是基础类型时，把它当作带 NetField 的嵌套数据对象。
            WriteData(writer, value, type);
        }
    }

    // WriteValue 的反向过程，读取顺序和类型必须与写入时完全一致。
    private static object? ReadValue(BinaryReader reader, Type type)
    {
        if (type == typeof(int))
        {
            return reader.ReadInt32();
        }

        if (type == typeof(long))
        {
            return reader.ReadInt64();
        }

        if (type == typeof(float))
        {
            return reader.ReadSingle();
        }

        if (type == typeof(bool))
        {
            return reader.ReadBoolean();
        }

        if (type == typeof(string))
        {
            return ReadString(reader);
        }

        if (type.IsEnum)
        {
            return Enum.ToObject(type, reader.ReadInt32());
        }

        if (type.IsArray)
        {
            return ReadArray(reader, type.GetElementType()!);
        }

        return ReadData(reader, type);
    }

    // 字符串先写 UTF-8 字节长度，再写实际内容；-1 表示 null。
    private static void WriteString(BinaryWriter writer, string? value)
    {
        if (value == null)
        {
            writer.Write(-1);
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string? ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        return length < 0 ? null : Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    // 数组先写元素数量，再逐个复用 WriteValue；-1 表示 null。
    private static void WriteArray(BinaryWriter writer, Array? values, Type elementType)
    {
        if (values == null)
        {
            writer.Write(-1);
            return;
        }

        writer.Write(values.Length);
        foreach (object? value in values)
        {
            WriteValue(writer, value, elementType);
        }
    }

    private static Array? ReadArray(BinaryReader reader, Type elementType)
    {
        int length = reader.ReadInt32();
        if (length < 0)
        {
            return null;
        }

        Array values = Array.CreateInstance(elementType, length);
        for (int i = 0; i < length; i++)
        {
            values.SetValue(ReadValue(reader, elementType), i);
        }

        return values;
    }

    // 嵌套对象先写一个 bool 表示是否存在，再递归处理其 NetField 字段。
    private static void WriteData(BinaryWriter writer, object? value, Type type)
    {
        writer.Write(value != null);
        if (value != null)
        {
            WriteObject(writer, value, type);
        }
    }

    private static object? ReadData(BinaryReader reader, Type type)
    {
        if (!reader.ReadBoolean())
        {
            return null;
        }

        object value = Activator.CreateInstance(type)!;
        ReadObject(reader, value, type);
        return value;
    }
}
