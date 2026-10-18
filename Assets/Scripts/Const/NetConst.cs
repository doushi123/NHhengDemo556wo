/// <summary>
/// 网络模块相关的常量。
/// </summary>
/// <remarks>
/// 注意：客户端与服务端是两个独立工程，常量各存一份，改的时候两边要同步。
/// </remarks>
public static class NetConst
{
    /// <summary>
    /// 单条消息体的最大字节数（1MB）。
    /// 收包时会用它校验长度，超长的包一律当作异常数据丢弃。
    /// </summary>
    public const int MaxMessageBodySize = 1024 * 1024;

    /// <summary>
    /// 客户端本地最多保留的世界聊天消息条数。
    /// </summary>
    public const int MaxWorldChatCount = 50;

    /// <summary>
    /// 服务端监听地址。目前固定本机
    /// </summary>
    public const string ServerIP = "127.0.0.1";

    /// <summary>
    /// 服务端监听端口。
    /// </summary>
    public const int ServerPort = 7777;

    /// <summary>
    /// 世界消息频道，一次消息输入的最多字符数
    /// </summary>
    public const int MaxChatCount=50;
}
