using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CarHub.Services;

public sealed class LocalAccountService
{
    private const int ResetCodeTtlMinutes = 15;
    private const string ResetSender = "CarrHub@hotmail.com";

    private readonly string _storePath;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private List<LocalAccountRecord> _accounts = [];

    public LocalAccountService(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _storePath = Path.Combine(dataDirectory, "accounts.json");
        Load();
    }

    public AuthResult Register(string email, string password, string confirmPassword)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
        {
            return AuthResult.Fail("Enter a valid email address.");
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
            if (_accounts.Any(account => account.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                return AuthResult.Fail("An account already exists for that email.");
            }

            var salt = GenerateSalt();
            var passwordHash = HashPassword(password, salt);

            _accounts.Add(new LocalAccountRecord
            {
                Email = normalizedEmail,
                PasswordSalt = Convert.ToBase64String(salt),
                PasswordHash = passwordHash,
                CreatedUtc = DateTimeOffset.UtcNow
            });

            SaveLocked();
        }

        return AuthResult.Ok(normalizedEmail, "Account created. You are now signed in.");
    }

    public AuthResult ValidateLogin(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        lock (_gate)
        {
            var account = FindAccountLocked(normalizedEmail);
            if (account is null)
            {
                return AuthResult.Fail("No user is made by that email.");
            }

            var salt = Convert.FromBase64String(account.PasswordSalt);
            var candidateHash = HashPassword(password, salt);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(candidateHash),
                    Convert.FromBase64String(account.PasswordHash)))
            {
                return AuthResult.Fail("That password is not correct.");
            }
        }

        return AuthResult.Ok(normalizedEmail, "Signed in successfully.");
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
            var account = FindAccountLocked(normalizedEmail);
            if (account is null)
            {
                return AuthResult.Fail("No user is made by that email.");
            }

            var code = RandomNumberGenerator.GetInt32(10000, 100000).ToString("D5");
            account.ResetCodeHash = HashResetCode(code);
            account.ResetCodeExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(ResetCodeTtlMinutes);
            account.ResetCodeUsedUtc = null;
            account.ResetRequestedUtc = DateTimeOffset.UtcNow;
            account.ResetSender = ResetSender;
            SaveLocked();

            return AuthResult.Ok(
                normalizedEmail,
                $"A new 5-digit reset code was prepared from {ResetSender}. The code expires in 15 minutes.",
                code,
                ResetSender,
                account.ResetCodeExpiresUtc);
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
            var account = FindAccountLocked(normalizedEmail);
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
                account.ResetCodeHash = string.Empty;
                account.ResetCodeExpiresUtc = null;
                account.ResetCodeUsedUtc = null;
                SaveLocked();
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
            account.PasswordSalt = Convert.ToBase64String(salt);
            account.PasswordHash = HashPassword(newPassword, salt);
            account.ResetCodeUsedUtc = DateTimeOffset.UtcNow;
            account.ResetCodeHash = string.Empty;
            account.ResetCodeExpiresUtc = null;
            SaveLocked();
        }

        return AuthResult.Ok(normalizedEmail, "Password updated successfully.");
    }

    private LocalAccountRecord? FindAccountLocked(string normalizedEmail)
    {
        return _accounts.FirstOrDefault(account => account.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));
    }

    private void Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_storePath))
            {
                _accounts = [];
                return;
            }

            var json = File.ReadAllText(_storePath);
            _accounts = JsonSerializer.Deserialize<List<LocalAccountRecord>>(json, _jsonOptions) ?? [];
        }
    }

    private void SaveLocked()
    {
        var json = JsonSerializer.Serialize(_accounts, _jsonOptions);
        File.WriteAllText(_storePath, json);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
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

    public sealed record AuthResult
    {
        public bool IsSuccess { get; init; }
        public string Email { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string? ResetCode { get; init; }
        public string? Sender { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }

        public static AuthResult Ok(string email, string message, string? resetCode = null, string? sender = null, DateTimeOffset? expiresAt = null)
        {
            return new AuthResult
            {
                IsSuccess = true,
                Email = email,
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

    private sealed class LocalAccountRecord
    {
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public string ResetCodeHash { get; set; } = string.Empty;
        public DateTimeOffset? ResetCodeExpiresUtc { get; set; }
        public DateTimeOffset? ResetCodeUsedUtc { get; set; }
        public DateTimeOffset? ResetRequestedUtc { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
        public string? ResetSender { get; set; }
    }
}
