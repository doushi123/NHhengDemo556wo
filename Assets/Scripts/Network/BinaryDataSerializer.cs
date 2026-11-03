using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

/// <summary>
/// 根据 NetField 自动读写数据对象，供 TCP 和 KCP 序列化器共用。
/// </summary>
public static class BinaryDataSerializer
{
    /// <summary>
    /// 网络字段缓存容器。
    /// </summary>
    private static ConcurrentDictionary<Type, FieldInfo[]> concurDic =
        new ConcurrentDictionary<Type, FieldInfo[]>();

    /// <summary>
    /// 按 NetField 的顺序写入对象中的所有网络字段。
    /// </summary>
    public static void Write(BinaryWriter writer, object value, Type type)
    {
        foreach (FieldInfo field in GetFields(type))
        {
            WriteValue(writer, field.GetValue(value), field.FieldType);
        }
    }

    /// <summary>
    /// 创建指定类型的对象，并按 NetField 的顺序读取所有网络字段。
    /// </summary>
    public static object Read(BinaryReader reader, Type type)
    {
        object value = Activator.CreateInstance(type);
        foreach (FieldInfo field in GetFields(type))
        {
            field.SetValue(value, ReadValue(reader, field.FieldType));
        }
        return value;
    }

    /// <summary>
    /// 获取并缓存指定类型中参与网络传输的字段。
    /// </summary>
    private static FieldInfo[] GetFields(Type type)
    {
        return concurDic.GetOrAdd(type, currentType => currentType // 缓存中没有该类型时，才执行后面的字段扫描。
            .GetFields( // 取得该类型中所有实例字段。
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Select(field => new // 把字段和它的 NetField 特性暂时放在一起，供后面过滤和排序。
            {
                Field = field,
                Attribute = field.GetCustomAttribute<NetFieldAttribute>(),
            })
            .Where(item => item.Attribute != null) // 只保留标记了 NetField 的网络字段。
            .OrderBy(item => item.Attribute.Order) // 按 NetField 中填写的编号决定传输顺序。
            .Select(item => item.Field) // 排序完成后只留下真正需要读写的字段信息。
            .ToArray()); // 保存成数组，供以后序列化和反序列化反复使用。
    }

    /// <summary>
    /// 根据字段类型选择对应的二进制写入方式。
    /// </summary>
    private static void WriteValue(BinaryWriter writer, object value, Type type)
    {
        if (type == typeof(int))
        {
            writer.Write((int)value);
        }
        else if (type == typeof(long))
        {
            writer.Write((long)value);
        }
        else if (type == typeof(float))
        {
            writer.Write((float)value);
        }
        else if (type == typeof(bool))
        {
            writer.Write((bool)value);
        }
        else if (type == typeof(string))
        {
            WriteString(writer, (string)value);
        }
        else if (type.IsEnum)
        {
            writer.Write(Convert.ToInt32(value));
        }
        else if (type.IsArray)
        {
            WriteArray(writer, (Array)value, type.GetElementType());
        }
        else
        {
            WriteData(writer, value, type);
        }
    }

    /// <summary>
    /// 根据字段类型选择对应的二进制读取方式。
    /// </summary>
    private static object ReadValue(BinaryReader reader, Type type)
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
            return ReadArray(reader, type.GetElementType());
        }
        return ReadData(reader, type);
    }

    /// <summary>
    /// 写入字符串；-1 表示 null。
    /// </summary>
    private static void WriteString(BinaryWriter writer, string value)
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

    /// <summary>
    /// 读取由 WriteString 写入的字符串。
    /// </summary>
    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        return length < 0 ? null : Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    /// <summary>
    /// 写入数组长度和所有数组元素；-1 表示 null。
    /// </summary>
    private static void WriteArray(BinaryWriter writer, Array values, Type elementType)
    {
        if (values == null)
        {
            writer.Write(-1);
            return;
        }

        writer.Write(values.Length);
        foreach (object value in values)
        {
            WriteValue(writer, value, elementType);
        }
    }

    /// <summary>
    /// 读取由 WriteArray 写入的数组。
    /// </summary>
    private static Array ReadArray(BinaryReader reader, Type elementType)
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

    /// <summary>
    /// 写入嵌套数据是否存在以及它的所有网络字段。
    /// </summary>
    private static void WriteData(BinaryWriter writer, object value, Type type)
    {
        writer.Write(value != null);
        if (value != null)
        {
            Write(writer, value, type);
        }
    }

    /// <summary>
    /// 读取由 WriteData 写入的嵌套数据。
    /// </summary>
    private static object ReadData(BinaryReader reader, Type type)
    {
        return reader.ReadBoolean() ? Read(reader, type) : null;
    }
}
