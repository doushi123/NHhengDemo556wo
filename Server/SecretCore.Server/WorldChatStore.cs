using System.Collections.Concurrent;
using System.Text.Json;

/// <summary>
/// 保存世界聊天频道最近的若干条消息。
/// 消息只在服务端产生：广播一条就记一条，玩家登录时把当前保留的部分一起下发。
/// </summary>
public sealed class WorldChatStore
{
    /// <summary>
    /// 世界聊天频道最多保留的消息条数。
    /// 必须与客户端 NetConst.MaxWorldChatCount 保持一致。
    /// </summary>
    public const int MaxWorldChatCount = 50;

    private readonly object dataLock = new object();
    private readonly string historyPath;
    private readonly BlockingCollection<WorldChatData[]> saveQueue = new BlockingCollection<WorldChatData[]>();
    private readonly Thread saveThread;
    private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        WriteIndented = true,
    };

    private readonly Queue<WorldChatData> messages = new Queue<WorldChatData>();
    private bool isClosed;

    public WorldChatStore(string dataDirectory)
    {
        historyPath = Path.Combine(dataDirectory, "WorldChat.json");
        Directory.CreateDirectory(dataDirectory);

        saveThread = new Thread(SaveMessages)
        {
            IsBackground = true,
            Name = "WorldChatSaveThread",
        };
        saveThread.Start();
    }

    public void Load()
    {
        lock (dataLock)
        {
            messages.Clear();

            if (!File.Exists(historyPath))
            {
                return;
            }

            WorldChatData[]? saved = JsonSerializer.Deserialize<WorldChatData[]>(
                File.ReadAllText(historyPath), jsonOptions);

            if (saved == null)
            {
                return;
            }

            foreach (WorldChatData data in saved)
            {
                messages.Enqueue(data);
            }
        }
    }

    /// <summary>
    /// 记入一条已经确认可以广播的消息，并保留最近 MaxWorldChatCount 条。
    /// </summary>
    public void Add(WorldChatData data)
    {
        lock (dataLock)
        {
            if (isClosed)
            {
                return;
            }

            messages.Enqueue(data);

            while (messages.Count > MaxWorldChatCount)
            {
                messages.Dequeue();
            }

            // 在修改消息队列的同一把锁中取得快照，保证保存任务与消息先后顺序一致。
            saveQueue.Add(messages.ToArray());
        }
    }

    /// <summary>
    /// 停止接收新的保存任务，并等待已经排队的聊天记录全部写入磁盘。
    /// </summary>
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

    public int Count
    {
        get
        {
            lock (dataLock)
            {
                return messages.Count;
            }
        }
    }

    public WorldChatData[] GetHistory()
    {
        lock (dataLock)
        {
            return messages.ToArray();
        }
    }

    /// <summary>
    /// 返回已保存消息中最大的消息 ID，供服务端在启动时接着编号。
    /// 否则重启后新消息的 ID 会从 1 重新开始，与历史消息重复。
    /// </summary>
    public long GetLatestMessageID()
    {
        lock (dataLock)
        {
            return messages.Count == 0 ? 0 : messages.Max(data => data.messageID);
        }
    }

    /// <summary>
    /// 只有这个线程会写 WorldChat.json，多个客户端提交的保存任务会按顺序执行。
    /// </summary>
    private void SaveMessages()
    {
        foreach (WorldChatData[] snapshot in saveQueue.GetConsumingEnumerable())
        {
            try
            {
                // 先写临时文件再替换，避免写一半时进程退出留下破损的正式存档。
                string tempPath = historyPath + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(snapshot, jsonOptions));
                File.Move(tempPath, historyPath, true);
            }
            catch (Exception exception)
            {
                Console.WriteLine("保存世界聊天记录失败：" + exception.Message);
            }
        }
    }
}
