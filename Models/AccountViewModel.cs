namespace CarHub.Models;

public sealed class AccountViewModel
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public string ResetCode { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string FormAction { get; set; } = "signup";

    public string Provider { get; set; } = "email";

    public string Message { get; set; } = string.Empty;

    public string MessageType { get; set; } = "info";

    public string? ResetSender { get; set; }

    public DateTimeOffset? ResetExpiresAt { get; set; }

    public string? GeneratedResetCode { get; set; }

    public bool IsAuthenticated { get; set; }

    public string? SignedInEmail { get; set; }
}
