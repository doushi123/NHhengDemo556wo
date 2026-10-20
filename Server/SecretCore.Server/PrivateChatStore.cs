using System.Collections.Concurrent;
using System.Text.Json;

/// <summary>
/// 按两个玩家组成的会话保存好友私聊历史，每个会话只保留最近 100 条。
/// </summary>
public sealed class PrivateChatStore
{
    public const int MaxPrivateChatCount = 100;

    private readonly object dataLock = new object();
    private readonly string historyPath;
    private readonly Dictionary<string, List<PrivateChatData>> conversations =
        new Dictionary<string, List<PrivateChatData>>();
    private readonly BlockingCollection<Dictionary<string, PrivateChatData[]>> saveQueue =
        new BlockingCollection<Dictionary<string, PrivateChatData[]>>();
    private readonly Thread saveThread;
    private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        WriteIndented = true,
    };

    private bool isClosed;

    public PrivateChatStore(string dataDirectory)
    {
        historyPath = Path.Combine(dataDirectory, "PrivateChat.json");
        Directory.CreateDirectory(dataDirectory);

        saveThread = new Thread(SaveMessages)
        {
            IsBackground = true,
            Name = "PrivateChatSaveThread",
        };
        saveThread.Start();
    }

    public void Load()
    {
        lock (dataLock)
        {
            conversations.Clear();

            if (!File.Exists(historyPath))
            {
                return;
            }

            Dictionary<string, PrivateChatData[]>? saved =
                JsonSerializer.Deserialize<Dictionary<string, PrivateChatData[]>>(
                    File.ReadAllText(historyPath), jsonOptions);

            if (saved == null)
            {
                return;
            }

            foreach ((string key, PrivateChatData[] messages) in saved)
            {
                conversations.Add(key, messages.TakeLast(MaxPrivateChatCount).ToList());
            }
        }
    }

    public void Add(PrivateChatData data)
    {
        lock (dataLock)
        {
            if (isClosed)
            {
                return;
            }

            string key = GetConversationKey(data.senderPlayerID, data.receiverPlayerID);
            if (!conversations.TryGetValue(key, out List<PrivateChatData>? messages))
            {
                messages = new List<PrivateChatData>();
                conversations.Add(key, messages);
            }

            messages.Add(data);
            if (messages.Count > MaxPrivateChatCount)
            {
                messages.RemoveAt(0);
            }

            saveQueue.Add(CreateSnapshot());
        }
    }

    public PrivateChatData[] GetHistory(int playerID)
    {
        lock (dataLock)
        {
            return conversations.Values
                .SelectMany(messages => messages)
                .Where(data => data.senderPlayerID == playerID || data.receiverPlayerID == playerID)
                .OrderBy(data => data.messageID)
                .ToArray();
        }
    }

    public long GetLatestMessageID()
    {
        lock (dataLock)
        {
            return conversations.Count == 0
                ? 0
                : conversations.Values.SelectMany(messages => messages)
                    .Select(data => data.messageID)
                    .DefaultIfEmpty(0)
                    .Max();
        }
    }

    public void Close()
    {
        lock (dataLock)
        {
            if (isClosed)
            {
                return;
            }

            isClosed = true;
            saveQueue.CompleteAdding();
        }

        saveThread.Join();
        saveQueue.Dispose();
    }

    private Dictionary<string, PrivateChatData[]> CreateSnapshot()
    {
        return conversations.ToDictionary(
            item => item.Key,
            item => item.Value.ToArray());
    }

    private void SaveMessages()
    {
        foreach (Dictionary<string, PrivateChatData[]> snapshot in saveQueue.GetConsumingEnumerable())
        {
            try
            {
                string tempPath = historyPath + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(snapshot, jsonOptions));
                File.Move(tempPath, historyPath, true);
            }
            catch (Exception exception)
            {
                Console.WriteLine("保存好友私聊记录失败：" + exception.Message);
            }
        }
    }

    private static string GetConversationKey(int firstPlayerID, int secondPlayerID)
    {
        int smallerID = Math.Min(firstPlayerID, secondPlayerID);
        int largerID = Math.Max(firstPlayerID, secondPlayerID);
        return smallerID + "_" + largerID;
    }
}
