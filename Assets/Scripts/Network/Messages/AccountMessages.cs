using System;

/// <summary>
/// 客户端发送给服务端的登录请求。
/// </summary>
[NetMessage(1201)]
public sealed class LoginRequestMsg : BaseMsg
{
    [NetField(1)]
    public string account = string.Empty;

    [NetField(2)]
    public string password = string.Empty;
}

/// <summary>
/// 客户端发送给服务端的注册请求。
/// </summary>
[NetMessage(1202)]
public sealed class RegisterRequestMsg : BaseMsg
{
    [NetField(1)]
    public string account = string.Empty;

    [NetField(2)]
    public string password = string.Empty;
}

/// <summary>
/// 登录或注册成功后进入大厅需要的首批数据。
/// 以后增加大厅初始化数据时统一放在这里，不继续增加认证消息的参数。
/// </summary>
public sealed class LoginInitialData
{
    [NetField(1)]
    public PlayerProfileData playerData;

    [NetField(2)]
    public PlayerSocialData socialData;

    [NetField(3)]
    public WorldChatData[] worldChatHistory = Array.Empty<WorldChatData>();
}

/// <summary>
/// 登录或注册成功后，服务端统一下发的认证成功消息。
/// 只包含客户端需要的数据，不包含密码哈希和盐。
/// </summary>
[NetMessage(1203)]
public sealed class AuthSuccessMsg : BaseMsg
{
    [NetField(1)]
    public LoginInitialData initialData;
}
