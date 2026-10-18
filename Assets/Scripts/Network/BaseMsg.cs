using System;

/// <summary>
/// 标记一个类是网络消息，并给它指定唯一的消息 ID。
/// 接收端先读取消息 ID，再找到应该创建哪一种消息。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class NetMessageAttribute : Attribute
{
    public int ID { get; }

    public NetMessageAttribute(int id)
    {
        ID = id;
    }
}

/// <summary>
/// 标记需要参与网络传输的字段。
/// Order 决定字段的写入和读取顺序，客户端与服务端必须保持一致。
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class NetFieldAttribute : Attribute
{
    public int Order { get; }

    public NetFieldAttribute(int order)
    {
        Order = order;
    }
}

/// <summary>
/// 所有网络消息的共同父类。
/// 字段读写已经交给 NetSerializer，子类只需要声明数据。
/// </summary>
public abstract class BaseMsg
{
}
