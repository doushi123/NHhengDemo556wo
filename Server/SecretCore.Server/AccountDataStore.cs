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
            playerSocialData = new PlayerSocialData();

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
            socialData.Add(playerID, playerSocialData);

            SaveAccounts();
            SaveSocialData();

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

            if (!socialData.TryGetValue(accountData.playerData.playerID, out playerSocialData))
            {
                playerSocialData = new PlayerSocialData();
                socialData.Add(accountData.playerData.playerID, playerSocialData);
                SaveSocialData();
            }

            SaveAccounts();

            playerData = accountData.playerData;
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
            return socialData.TryGetValue(playerID, out playerSocialData);
        }
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
