string dataDirectory = GetDataDirectory();
BattleKcpServer battleServer = new BattleKcpServer();
ServerSocket serverSocket = new ServerSocket(dataDirectory, battleServer);
serverSocket.Start(7777);
battleServer.Start(7778);

Console.WriteLine("服务端已启动，按回车键关闭。");
Console.WriteLine("存档目录：" + dataDirectory);
Console.ReadLine();

battleServer.Close();
serverSocket.Close();

static string GetDataDirectory()
{
    DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "SecretCore.Server.csproj")))
        {
            return Path.Combine(directory.FullName, "ServerData");
        }

        directory = directory.Parent;
    }

    return Path.Combine(AppContext.BaseDirectory, "ServerData");
}
