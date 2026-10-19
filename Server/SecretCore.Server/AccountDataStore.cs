using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// 负责账号与社交数据的内存查询和 JSON 持久化。
/// </summary>
public sealed class AccountDataStore
{
    private const int FirstPlayerID = 10001;
    private const int PasswordIterations = 100000;

    private readonly object dataLock = new object();
    private readonly string accountsPath;
    private readonly string socialDataPath;
    private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        WriteIndented = true,
    };

    private Dictionary<string, AccountData> accounts =
        new Dictionary<string, AccountData>(StringComparer.Ordinal);
    private Dictionary<int, PlayerSocialData> socialData =
        new Dictionary<int, PlayerSocialData>();
    private int nextPlayerID = FirstPlayerID;

    public AccountDataStore(string dataDirectory)
    {
        accountsPath = Path.Combine(dataDirectory, "Accounts.json");
        socialDataPath = Path.Combine(dataDirectory, "SocialData.json");
        Directory.CreateDirectory(dataDirectory);
    }

    public void Load()
    {
        lock (dataLock)
        {
            if (File.Exists(accountsPath))
            {
                Dictionary<string, AccountData>? savedAccounts =
                    JsonSerializer.Deserialize<Dictionary<string, AccountData>>(
                        File.ReadAllText(accountsPath), jsonOptions);

                accounts = savedAccounts == null
                    ? new Dictionary<string, AccountData>(StringComparer.Ordinal)
                    : new Dictionary<string, AccountData>(savedAccounts, StringComparer.Ordinal);
            }

            if (File.Exists(socialDataPath))
            {
                socialData = JsonSerializer.Deserialize<Dictionary<int, PlayerSocialData>>(
                    File.ReadAllText(socialDataPath), jsonOptions)
                    ?? new Dictionary<int, PlayerSocialData>();
            }

            nextPlayerID = accounts.Count == 0
                ? FirstPlayerID
                : accounts.Values.Max(data => data.playerData.playerID) + 1;

        }
    }

    public bool TryRegister(
        string account,
        string password,
        out PlayerProfileData? playerData,
        out PlayerSocialData? playerSocialData,
        out string error)
    {
        account = account.Trim();

        lock (dataLock)
        {
            if (account.Length == 0 || password.Length == 0)
            {
                playerData = null;
                playerSocialData = null;
                error = "账号和密码不能为空。";
                return false;
            }

            if (accounts.ContainsKey(account))
            {
                playerData = null;
                playerSocialData = null;
                error = "该账号已经存在。";
                return false;
            }

            int playerID = nextPlayerID++;
            string passwordSalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

            playerData = new PlayerProfileData
            {
                playerID = playerID,
                account = account,
                state = PlayerOnlineState.Lobby,
                level = 1,
            };
            PlayerSocialData savedSocialData = new PlayerSocialData();

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            accounts.Add(account, new AccountData
            {
                account = account,
                passwordSalt = passwordSalt,
                passwordHash = HashPassword(password, passwordSalt),
                createTime = now,
                lastLoginTime = now,
                playerData = playerData,
            });
            socialData.Add(playerID, savedSocialData);

            SaveAccounts();
            SaveSocialData();

            playerSocialData = CreatePlayerSocialData(savedSocialData);
            error = string.Empty;
            return true;
        }
    }

    public bool TryLogin(
        string account,
        string password,
        out PlayerProfileData? playerData,
        out PlayerSocialData? playerSocialData,
        out string error)
    {
        account = account.Trim();

        lock (dataLock)
        {
            if (!accounts.TryGetValue(account, out AccountData? accountData)
                || !VerifyPassword(password, accountData.passwordSalt, accountData.passwordHash))
            {
                playerData = null;
                playerSocialData = null;
                error = "账号或密码错误。";
                return false;
            }

            accountData.lastLoginTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            accountData.playerData.state = PlayerOnlineState.Lobby;

            if (!socialData.TryGetValue(accountData.playerData.playerID, out PlayerSocialData? savedSocialData))
            {
                savedSocialData = new PlayerSocialData();
                socialData.Add(accountData.playerData.playerID, savedSocialData);
                SaveSocialData();
            }

            SaveAccounts();

            playerData = accountData.playerData;
            playerSocialData = CreatePlayerSocialData(savedSocialData);
            error = string.Empty;
            return true;
        }
    }

    public bool TryGetPlayerData(int playerID, out PlayerProfileData? playerData)
    {
        lock (dataLock)
        {
            AccountData? accountData = accounts.Values.FirstOrDefault(
                data => data.playerData.playerID == playerID);
            playerData = accountData?.playerData;
            return playerData != null;
        }
    }

    public bool TryGetFriendsData(int playerID, out PlayerSocialData? playerSocialData)
    {
        lock (dataLock)
        {
            if (!socialData.TryGetValue(playerID, out PlayerSocialData? savedSocialData))
            {
                playerSocialData = null;
                return false;
            }

            playerSocialData = CreatePlayerSocialData(savedSocialData);
            return true;
        }
    }

    public bool TrySearchPlayer(string account, out PlayerProfileData? playerData, out string error)
    {
        account = account.Trim();

        lock (dataLock)
        {
            if (account.Length == 0)
            {
                playerData = null;
                error = "请输入要查找的玩家账号。";
                return false;
            }

            if (!accounts.TryGetValue(account, out AccountData? accountData))
            {
                playerData = null;
                error = "没有找到该玩家。";
                return false;
            }

            playerData = accountData.playerData;
            error = string.Empty;
            return true;
        }
    }

    public bool TrySendFriendRequest(
        int senderPlayerID,
        int targetPlayerID,
        string message,
        out PlayerSocialData? targetSocialData,
        out string error)
    {
        message = message.Trim();

        lock (dataLock)
        {
            if (senderPlayerID == targetPlayerID)
            {
                targetSocialData = null;
                error = "不能添加自己为好友。";
                return false;
            }

            if (message.Length > 50)
            {
                targetSocialData = null;
                error = "好友申请留言最多输入 50 个字符。";
                return false;
            }

            if (FindAccountByPlayerID(targetPlayerID) == null)
            {
                targetSocialData = null;
                error = "没有找到申请目标。";
                return false;
            }

            PlayerSocialData senderSocialData = socialData[senderPlayerID];
            PlayerSocialData targetSavedSocialData = socialData[targetPlayerID];

            if (senderSocialData.friends.Any(data => data.friendPlayerID == targetPlayerID))
            {
                targetSocialData = null;
                error = "你们已经是好友。";
                return false;
            }

            if (targetSavedSocialData.friendRequests.Any(data => data.senderPlayerID == senderPlayerID))
            {
                targetSocialData = null;
                error = "已经向该玩家发送过好友申请。";
                return false;
            }

            if (senderSocialData.friendRequests.Any(data => data.senderPlayerID == targetPlayerID))
            {
                targetSocialData = null;
                error = "对方已经向你发送了好友申请，请先处理。";
                return false;
            }

            List<FriendRequestData> requests = targetSavedSocialData.friendRequests.ToList();
            requests.Add(new FriendRequestData
            {
                senderPlayerID = senderPlayerID,
                message = message,
                createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });
            targetSavedSocialData.friendRequests = requests.ToArray();

            SaveSocialData();
            targetSocialData = CreatePlayerSocialData(targetSavedSocialData);
            error = string.Empty;
            return true;
        }
    }

    public bool TryHandleFriendRequest(
        int receiverPlayerID,
        int senderPlayerID,
        bool accept,
        out PlayerSocialData? receiverSocialData,
        out PlayerSocialData? senderSocialData,
        out string error)
    {
        lock (dataLock)
        {
            PlayerSocialData receiverSavedData = socialData[receiverPlayerID];
            FriendRequestData? request = receiverSavedData.friendRequests
                .FirstOrDefault(data => data.senderPlayerID == senderPlayerID);

            if (request == null)
            {
                receiverSocialData = null;
                senderSocialData = null;
                error = "该好友申请已经不存在。";
                return false;
            }

            PlayerSocialData senderSavedData = socialData[senderPlayerID];

            receiverSavedData.friendRequests = receiverSavedData.friendRequests
                .Where(data => data.senderPlayerID != senderPlayerID)
                .ToArray();

            if (accept)
            {
                AddFriend(receiverSavedData, senderPlayerID);
                AddFriend(senderSavedData, receiverPlayerID);
            }

            SaveSocialData();
            receiverSocialData = CreatePlayerSocialData(receiverSavedData);
            senderSocialData = CreatePlayerSocialData(senderSavedData);
            error = string.Empty;
            return true;
        }
    }

    private void AddFriend(PlayerSocialData social, int friendPlayerID)
    {
        if (social.friends.Any(data => data.friendPlayerID == friendPlayerID))
        {
            return;
        }

        List<FriendRelationData> friends = social.friends.ToList();
        friends.Add(new FriendRelationData { friendPlayerID = friendPlayerID });
        social.friends = friends.ToArray();
    }

    private PlayerSocialData CreatePlayerSocialData(PlayerSocialData savedSocialData)
    {
        FriendRelationData[] friends = savedSocialData.friends
            .Select(relation => new
            {
                relation,
                account = FindAccountByPlayerID(relation.friendPlayerID),
            })
            .Where(item => item.account != null)
            .Select(item => new FriendRelationData
            {
                friendPlayerID = item.relation.friendPlayerID,
                playerData = item.account!.playerData,
                remark = item.relation.remark,
                isFavorite = item.relation.isFavorite,
            })
            .ToArray();

        FriendRequestData[] requests = savedSocialData.friendRequests
            .Select(request => new
            {
                request,
                account = FindAccountByPlayerID(request.senderPlayerID),
            })
            .Where(item => item.account != null)
            .Select(item => new FriendRequestData
            {
                senderPlayerID = item.request.senderPlayerID,
                senderData = item.account!.playerData,
                message = item.request.message,
                createTime = item.request.createTime,
            })
            .ToArray();

        return new PlayerSocialData
        {
            friends = friends,
            friendRequests = requests,
        };
    }

    private AccountData? FindAccountByPlayerID(int playerID)
    {
        return accounts.Values.FirstOrDefault(data => data.playerData.playerID == playerID);
    }

    private string HashPassword(string password, string passwordSalt)
    {
        byte[] saltBytes = Convert.FromBase64String(passwordSalt);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password, saltBytes, PasswordIterations, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(hashBytes);
    }

    private bool VerifyPassword(string password, string passwordSalt, string passwordHash)
    {
        byte[] actualHash = Convert.FromBase64String(HashPassword(password, passwordSalt));
        byte[] savedHash = Convert.FromBase64String(passwordHash);
        return CryptographicOperations.FixedTimeEquals(actualHash, savedHash);
    }

    private void SaveAccounts()
    {
        File.WriteAllText(accountsPath, JsonSerializer.Serialize(accounts, jsonOptions));
    }

    private void SaveSocialData()
    {
        File.WriteAllText(socialDataPath, JsonSerializer.Serialize(socialData, jsonOptions));
    }
}
