using Microsoft.Data.Sqlite;
using SQLitePCL;
using System.Security.Cryptography;
using System.Text;

namespace CarHub.Services;

public sealed class LocalAccountService
{
    private const int ResetCodeTtlMinutes = 15;
    private const string ResetSender = "CarrHub@hotmail.com";

    private readonly string _connectionString;
    private readonly object _gate = new();

    public LocalAccountService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        Batteries_V2.Init();

        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);

        _connectionString = configuration.GetConnectionString("SQLiteConnection") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _connectionString = $"Data Source={Path.Combine(dataDirectory, "accounts.db")}";
        }

        InitializeDatabase();
    }

    public AuthResult Register(string email, string password, string confirmPassword)
        => Register(email, password, confirmPassword, null, false);

    public AuthResult Register(string email, string password, string confirmPassword, string? displayName, bool acceptTerms)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
        {
            return AuthResult.Fail("Enter a valid email address.");
        }

        if (!acceptTerms)
        {
            return AuthResult.Fail("You must accept the Terms of Use and Privacy Policy before creating a CarHub account.");
        }

        if (password.Length < 6)
        {
            return AuthResult.Fail("Choose a password with at least 6 characters.");
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return AuthResult.Fail("Passwords do not match.");
        }

        lock (_gate)
        {
            using var connection = OpenConnection();
            var existing = GetProfileLocked(connection, normalizedEmail, null, null);
            var userName = NormalizeUserName(displayName, normalizedEmail);
            var now = DateTimeOffset.UtcNow;
            var salt = GenerateSalt();
            var passwordHash = HashPassword(password, salt);

            if (existing is null)
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO Users (
                        Email, UserName, DisplayName, Provider, ProviderKey,
                        PasswordHash, PasswordSalt, ProfileCustomized,
                        TwoFactorEnabled, PasskeyCount, ResetCodeHash,
                        ResetCodeExpiresUtc, ResetCodeUsedUtc, ResetRequestedUtc,
                        ResetSender, CreatedUtc, LastLoginUtc
                    ) VALUES (
                        $email, $userName, $displayName, 'local', NULL,
                        $passwordHash, $passwordSalt, 0,
                        0, 0, NULL,
                        NULL, NULL, NULL,
                        NULL, $createdUtc, $lastLoginUtc
                    );
                    """;
                command.Parameters.AddWithValue("$email", normalizedEmail);
                command.Parameters.AddWithValue("$userName", userName);
                command.Parameters.AddWithValue("$displayName", userName);
                command.Parameters.AddWithValue("$passwordHash", passwordHash);
                command.Parameters.AddWithValue("$passwordSalt", Convert.ToBase64String(salt));
                command.Parameters.AddWithValue("$createdUtc", ToDbDateTime(now));
                command.Parameters.AddWithValue("$lastLoginUtc", ToDbDateTime(now));
                command.ExecuteNonQuery();
            }
            else if (string.IsNullOrWhiteSpace(existing.PasswordHash))
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE Users
                    SET PasswordHash = $passwordHash,
                        PasswordSalt = $passwordSalt,
                        UserName = COALESCE(NULLIF(UserName, ''), $userName),
                        DisplayName = COALESCE(NULLIF(DisplayName, ''), $displayName),
                        LastLoginUtc = $lastLoginUtc
                    WHERE Email = $email;
                    """;
                command.Parameters.AddWithValue("$passwordHash", passwordHash);
                command.Parameters.AddWithValue("$passwordSalt", Convert.ToBase64String(salt));
                command.Parameters.AddWithValue("$userName", userName);
                command.Parameters.AddWithValue("$displayName", userName);
                command.Parameters.AddWithValue("$lastLoginUtc", ToDbDateTime(now));
                command.Parameters.AddWithValue("$email", normalizedEmail);
                command.ExecuteNonQuery();
            }
            else
            {
                return AuthResult.Fail("An account already exists for that email.");
            }
        }

        return GetSuccessResult(normalizedEmail, "Account created. You are now signed in.");
    }

    public AuthResult ValidateLogin(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        lock (_gate)
        {
            using var connection = OpenConnection();
            var account = GetProfileLocked(connection, normalizedEmail, null, null);
            if (account is null)
            {
                return AuthResult.Fail("No user is made by that email.");
            }

            if (string.IsNullOrWhiteSpace(account.PasswordHash) || string.IsNullOrWhiteSpace(account.PasswordSalt))
            {
                return AuthResult.Fail("That email is linked to a Google or GitHub account. Use social sign-in.");
            }

            var salt = Convert.FromBase64String(account.PasswordSalt);
            var candidateHash = HashPassword(password, salt);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(candidateHash),
                    Convert.FromBase64String(account.PasswordHash)))
            {
                return AuthResult.Fail("That password is not correct.");
            }

            TouchLoginLocked(connection, account.Email);
            account = GetProfileLocked(connection, normalizedEmail, null, null) ?? account;
            return AuthResult.Ok(account, "Signed in successfully.");
        }
    }

    public AuthResult RequestResetCode(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
        {
            return AuthResult.Fail("Enter the email address for the account you want to reset.");
        }

        lock (_gate)
        {
            using var connection = OpenConnection();
            var account = GetProfileLocked(connection, normalizedEmail, null, null);
            if (account is null)
            {
                return AuthResult.Fail("No user is made by that email.");
            }

            var code = RandomNumberGenerator.GetInt32(10000, 100000).ToString("D5");
            var expiresUtc = DateTimeOffset.UtcNow.AddMinutes(ResetCodeTtlMinutes);

            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Users
                SET ResetCodeHash = $resetCodeHash,
                    ResetCodeExpiresUtc = $expiresUtc,
                    ResetCodeUsedUtc = NULL,
                    ResetRequestedUtc = $requestedUtc,
                    ResetSender = $sender
                WHERE Email = $email;
                """;
            command.Parameters.AddWithValue("$resetCodeHash", HashResetCode(code));
            command.Parameters.AddWithValue("$expiresUtc", ToDbDateTime(expiresUtc));
            command.Parameters.AddWithValue("$requestedUtc", ToDbDateTime(DateTimeOffset.UtcNow));
            command.Parameters.AddWithValue("$sender", ResetSender);
            command.Parameters.AddWithValue("$email", normalizedEmail);
            command.ExecuteNonQuery();

            return AuthResult.Ok(
                account,
                $"A new 5-digit reset code was prepared from {ResetSender}. The code expires in 15 minutes.",
                code,
                ResetSender,
                expiresUtc);
        }
    }

    public AuthResult ResetPassword(string email, string resetCode, string newPassword, string confirmPassword)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
        {
            return AuthResult.Fail("Enter a valid email address.");
        }

        if (newPassword.Length < 6)
        {
            return AuthResult.Fail("Choose a new password with at least 6 characters.");
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            return AuthResult.Fail("New password and confirmation do not match.");
        }

        lock (_gate)
        {
            using var connection = OpenConnection();
            var account = GetProfileLocked(connection, normalizedEmail, null, null);
            if (account is null)
            {
                return AuthResult.Fail("No user is made by that email.");
            }

            if (string.IsNullOrWhiteSpace(account.ResetCodeHash) || account.ResetCodeExpiresUtc is null)
            {
                return AuthResult.Fail("Request a new reset code first.");
            }

            if (account.ResetCodeUsedUtc is not null)
            {
                return AuthResult.Fail("That reset code was already used. Request a new one.");
            }

            if (DateTimeOffset.UtcNow > account.ResetCodeExpiresUtc.Value)
            {
                ClearResetCodeLocked(connection, normalizedEmail);
                return AuthResult.Fail("That reset code expired after 15 minutes. Request a new one.");
            }

            var candidate = resetCode?.Trim() ?? string.Empty;
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(HashResetCode(candidate)),
                    Convert.FromBase64String(account.ResetCodeHash)))
            {
                return AuthResult.Fail("The reset code is not correct.");
            }

            var salt = GenerateSalt();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Users
                SET PasswordSalt = $passwordSalt,
                    PasswordHash = $passwordHash,
                    ResetCodeUsedUtc = $usedUtc,
                    ResetCodeHash = NULL,
                    ResetCodeExpiresUtc = NULL
                WHERE Email = $email;
                """;
            command.Parameters.AddWithValue("$passwordSalt", Convert.ToBase64String(salt));
            command.Parameters.AddWithValue("$passwordHash", HashPassword(newPassword, salt));
            command.Parameters.AddWithValue("$usedUtc", ToDbDateTime(DateTimeOffset.UtcNow));
            command.Parameters.AddWithValue("$email", normalizedEmail);
            command.ExecuteNonQuery();
        }

        return GetSuccessResult(normalizedEmail, "Password updated successfully.");
    }

    public AuthResult UpsertExternalAccount(string provider, string providerKey, string? email, string? displayName, string? username, string? pictureUrl)
    {
        var normalizedProvider = NormalizeProvider(provider);
        var normalizedProviderKey = providerKey.Trim();
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            normalizedEmail = $"{NormalizeUserName(username ?? displayName, normalizedProvider)}@{normalizedProvider}.carhub.local";
        }

        var userName = NormalizeUserName(displayName ?? username, normalizedEmail);
        var now = DateTimeOffset.UtcNow;

        lock (_gate)
        {
            using var connection = OpenConnection();
            var account = GetProfileLocked(connection, normalizedEmail, normalizedProvider, normalizedProviderKey);
            if (account is null)
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO Users (
                        Email, UserName, DisplayName, Provider, ProviderKey,
                        PasswordHash, PasswordSalt, ProfileCustomized,
                        TwoFactorEnabled, PasskeyCount, PictureUrl,
                        ResetCodeHash, ResetCodeExpiresUtc, ResetCodeUsedUtc,
                        ResetRequestedUtc, ResetSender, CreatedUtc, LastLoginUtc
                    ) VALUES (
                        $email, $userName, $displayName, $provider, $providerKey,
                        NULL, NULL, 0,
                        0, 0, $pictureUrl,
                        NULL, NULL, NULL,
                        NULL, NULL, $createdUtc, $lastLoginUtc
                    );
                    """;
                command.Parameters.AddWithValue("$email", normalizedEmail);
                command.Parameters.AddWithValue("$userName", userName);
                command.Parameters.AddWithValue("$displayName", userName);
                command.Parameters.AddWithValue("$provider", normalizedProvider);
                command.Parameters.AddWithValue("$providerKey", normalizedProviderKey);
                command.Parameters.AddWithValue("$pictureUrl", pictureUrl ?? string.Empty);
                command.Parameters.AddWithValue("$createdUtc", ToDbDateTime(now));
                command.Parameters.AddWithValue("$lastLoginUtc", ToDbDateTime(now));
                command.ExecuteNonQuery();
            }
            else
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE Users
                    SET Provider = $provider,
                        ProviderKey = $providerKey,
                        Email = $email,
                        UserName = CASE
                            WHEN ProfileCustomized = 1 THEN UserName
                            ELSE COALESCE(NULLIF($userName, ''), UserName)
                        END,
                        DisplayName = CASE
                            WHEN ProfileCustomized = 1 THEN DisplayName
                            ELSE COALESCE(NULLIF($displayName, ''), DisplayName)
                        END,
                        PictureUrl = CASE
                            WHEN COALESCE(NULLIF(PictureUrl, ''), '') = '' THEN $pictureUrl
                            ELSE PictureUrl
                        END,
                        LastLoginUtc = $lastLoginUtc
                    WHERE Email = $email OR (Provider = $provider AND ProviderKey = $providerKey);
                    """;
                command.Parameters.AddWithValue("$provider", normalizedProvider);
                command.Parameters.AddWithValue("$providerKey", normalizedProviderKey);
                command.Parameters.AddWithValue("$email", normalizedEmail);
                command.Parameters.AddWithValue("$userName", userName);
                command.Parameters.AddWithValue("$displayName", userName);
                command.Parameters.AddWithValue("$pictureUrl", pictureUrl ?? string.Empty);
                command.Parameters.AddWithValue("$lastLoginUtc", ToDbDateTime(now));
                command.ExecuteNonQuery();
            }

            var profile = GetProfileLocked(connection, normalizedEmail, normalizedProvider, normalizedProviderKey)
                ?? GetProfileLocked(connection, normalizedEmail, null, null);

            return profile is null
                ? AuthResult.Fail("We could not finish the social sign-in.")
                : AuthResult.Ok(profile, $"Signed in with {profile.Email}.");
        }
    }

    public AuthResult UpdateProfile(string email, string? displayName)
    {
        var normalizedEmail = NormalizeEmail(email);
        var cleanedDisplayName = NormalizeUserName(displayName, normalizedEmail);
        if (string.IsNullOrWhiteSpace(cleanedDisplayName))
        {
            return AuthResult.Fail("Enter a username to save.");
        }

        lock (_gate)
        {
            using var connection = OpenConnection();
            var profile = GetProfileLocked(connection, normalizedEmail, null, null);
            if (profile is null)
            {
                return AuthResult.Fail("No user is made by that email.");
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Users
                SET UserName = $userName,
                    DisplayName = $displayName,
                    ProfileCustomized = 1
                WHERE Email = $email;
                """;
            command.Parameters.AddWithValue("$userName", cleanedDisplayName);
            command.Parameters.AddWithValue("$displayName", cleanedDisplayName);
            command.Parameters.AddWithValue("$email", normalizedEmail);
            command.ExecuteNonQuery();

            profile = GetProfileLocked(connection, normalizedEmail, null, null) ?? profile;
            return AuthResult.Ok(profile, "Profile settings saved.");
        }
    }

    public AuthResult ToggleTwoFactor(string email, bool enabled)
    {
        var normalizedEmail = NormalizeEmail(email);

        lock (_gate)
        {
            using var connection = OpenConnection();
            var profile = GetProfileLocked(connection, normalizedEmail, null, null);
            if (profile is null)
            {
                return AuthResult.Fail("No user is made by that email.");
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Users
                SET TwoFactorEnabled = $enabled
                WHERE Email = $email;
                """;
            command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
            command.Parameters.AddWithValue("$email", normalizedEmail);
            command.ExecuteNonQuery();

            profile = GetProfileLocked(connection, normalizedEmail, null, null) ?? profile;
            return AuthResult.Ok(profile, enabled ? "Two-factor is now enabled." : "Two-factor is now disabled.");
        }
    }

    public AccountProfile? GetProfile(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        lock (_gate)
        {
            using var connection = OpenConnection();
            return GetProfileLocked(connection, normalizedEmail, null, null);
        }
    }

    private void InitializeDatabase()
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Email TEXT NOT NULL UNIQUE,
                    UserName TEXT NOT NULL,
                    DisplayName TEXT NOT NULL,
                    Provider TEXT NOT NULL,
                    ProviderKey TEXT NULL,
                    PasswordHash TEXT NULL,
                    PasswordSalt TEXT NULL,
                    ProfileCustomized INTEGER NOT NULL DEFAULT 0,
                    TwoFactorEnabled INTEGER NOT NULL DEFAULT 0,
                    PasskeyCount INTEGER NOT NULL DEFAULT 0,
                    PictureUrl TEXT NULL,
                    ResetCodeHash TEXT NULL,
                    ResetCodeExpiresUtc TEXT NULL,
                    ResetCodeUsedUtc TEXT NULL,
                    ResetRequestedUtc TEXT NULL,
                    ResetSender TEXT NULL,
                    CreatedUtc TEXT NOT NULL,
                    LastLoginUtc TEXT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_ProviderKey
                ON Users (Provider, ProviderKey);
                """;
            command.ExecuteNonQuery();
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private AccountProfile? GetProfileLocked(SqliteConnection connection, string email, string? provider, string? providerKey)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Email, UserName, DisplayName, Provider, ProviderKey, PasswordHash, PasswordSalt,
                   ProfileCustomized, TwoFactorEnabled, PasskeyCount, PictureUrl,
                   ResetCodeHash, ResetCodeExpiresUtc, ResetCodeUsedUtc, ResetRequestedUtc,
                   ResetSender, CreatedUtc, LastLoginUtc
            FROM Users
            WHERE Email = $email
               OR ($provider IS NOT NULL AND $providerKey IS NOT NULL AND Provider = $provider AND ProviderKey = $providerKey)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$email", email);
        command.Parameters.AddWithValue("$provider", (object?)provider ?? DBNull.Value);
        command.Parameters.AddWithValue("$providerKey", (object?)providerKey ?? DBNull.Value);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return ReadProfile(reader);
    }

    private static AccountProfile ReadProfile(SqliteDataReader reader)
    {
        return new AccountProfile(
            Email: reader.GetString(0),
            UserName: reader.GetString(1),
            DisplayName: reader.GetString(2),
            Provider: reader.GetString(3),
            ProviderKey: reader.IsDBNull(4) ? null : reader.GetString(4),
            PasswordHash: reader.IsDBNull(5) ? null : reader.GetString(5),
            PasswordSalt: reader.IsDBNull(6) ? null : reader.GetString(6),
            ProfileCustomized: reader.GetInt32(7) == 1,
            TwoFactorEnabled: reader.GetInt32(8) == 1,
            PasskeyCount: reader.GetInt32(9),
            PictureUrl: reader.IsDBNull(10) ? null : reader.GetString(10),
            ResetCodeHash: reader.IsDBNull(11) ? null : reader.GetString(11),
            ResetCodeExpiresUtc: ReadNullableDateTimeOffset(reader, 12),
            ResetCodeUsedUtc: ReadNullableDateTimeOffset(reader, 13),
            ResetRequestedUtc: ReadNullableDateTimeOffset(reader, 14),
            ResetSender: reader.IsDBNull(15) ? null : reader.GetString(15),
            CreatedUtc: ReadDateTimeOffset(reader, 16),
            LastLoginUtc: ReadNullableDateTimeOffset(reader, 17));
    }

    private static DateTimeOffset ReadDateTimeOffset(SqliteDataReader reader, int ordinal)
    {
        return DateTimeOffset.Parse(reader.GetString(ordinal), null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    private static DateTimeOffset? ReadNullableDateTimeOffset(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : DateTimeOffset.Parse(reader.GetString(ordinal), null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    private void TouchLoginLocked(SqliteConnection connection, string email)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Users
            SET LastLoginUtc = $lastLoginUtc
            WHERE Email = $email;
            """;
        command.Parameters.AddWithValue("$lastLoginUtc", ToDbDateTime(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$email", email);
        command.ExecuteNonQuery();
    }

    private void ClearResetCodeLocked(SqliteConnection connection, string email)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Users
            SET ResetCodeHash = NULL,
                ResetCodeExpiresUtc = NULL,
                ResetCodeUsedUtc = NULL
            WHERE Email = $email;
            """;
        command.Parameters.AddWithValue("$email", email);
        command.ExecuteNonQuery();
    }

    private AuthResult GetSuccessResult(string email, string message)
    {
        var profile = GetProfile(email);
        return profile is null ? AuthResult.Fail(message) : AuthResult.Ok(profile, message);
    }

    private static string NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
    }

    private static string NormalizeProvider(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider) ? "local" : provider.Trim().ToLowerInvariant();
    }

    private static string NormalizeUserName(string? userName, string? email)
    {
        var source = string.IsNullOrWhiteSpace(userName)
            ? (string.IsNullOrWhiteSpace(email) ? "carhub-user" : email.Split('@', 2)[0])
            : userName.Trim();

        var builder = new StringBuilder(source.Length);
        foreach (var ch in source)
        {
            if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '_' || ch == '-' || ch == '.')
            {
                builder.Append(ch);
            }
        }

        var cleaned = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "carhub-user" : cleaned;
    }

    private static bool IsValidEmail(string email)
    {
        return !string.IsNullOrWhiteSpace(email)
            && email.Contains('@', StringComparison.Ordinal)
            && email.Contains('.', StringComparison.Ordinal);
    }

    private static byte[] GenerateSalt()
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }

    private static string HashPassword(string password, byte[] salt)
    {
        return Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32));
    }

    private static string HashResetCode(string code)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(code);
        return Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    private static string ToDbDateTime(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O");
    }

    public sealed record AccountProfile(
        string Email,
        string UserName,
        string DisplayName,
        string Provider,
        string? ProviderKey,
        string? PasswordHash,
        string? PasswordSalt,
        bool ProfileCustomized,
        bool TwoFactorEnabled,
        int PasskeyCount,
        string? PictureUrl,
        string? ResetCodeHash,
        DateTimeOffset? ResetCodeExpiresUtc,
        DateTimeOffset? ResetCodeUsedUtc,
        DateTimeOffset? ResetRequestedUtc,
        string? ResetSender,
        DateTimeOffset CreatedUtc,
        DateTimeOffset? LastLoginUtc);

    public sealed record AuthResult
    {
        public bool IsSuccess { get; init; }
        public string Email { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Provider { get; init; } = "local";
        public bool TwoFactorEnabled { get; init; }
        public int PasskeyCount { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? ResetCode { get; init; }
        public string? Sender { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }

        public static AuthResult Ok(AccountProfile profile, string message, string? resetCode = null, string? sender = null, DateTimeOffset? expiresAt = null)
        {
            return new AuthResult
            {
                IsSuccess = true,
                Email = profile.Email,
                UserName = profile.UserName,
                DisplayName = profile.DisplayName,
                Provider = profile.Provider,
                TwoFactorEnabled = profile.TwoFactorEnabled,
                PasskeyCount = profile.PasskeyCount,
                Message = message,
                ResetCode = resetCode,
                Sender = sender,
                ExpiresAt = expiresAt
            };
        }

        public static AuthResult Fail(string message)
        {
            return new AuthResult
            {
                IsSuccess = false,
                Message = message
            };
        }
    }
}
