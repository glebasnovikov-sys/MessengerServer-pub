using System.ComponentModel.DataAnnotations;

namespace MessengerServer.Models;

public class User
{
    public int Id { get; set; }
    [Required] public string Tag { get; set; } = string.Empty;
    [Required] public string DisplayName { get; set; } = string.Empty;
    [Required] public string PasswordHash { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarColor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Message
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    [Required] public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
}

// DTO — что отдаём клиенту (без PasswordHash)
public class UserDto
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarColor { get; set; }
}

public class RegisterRequest
{
    public string Tag { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Bio { get; set; }
}

public class LoginRequest
{
    public string Tag { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class SendMessageRequest
{
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
}
