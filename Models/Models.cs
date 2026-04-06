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
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool IsOnline { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DeletedChat
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PeerId { get; set; }
    public DateTime DeletedAt { get; set; }
}

public class Message
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    [Required] public string Text { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string MessageType { get; set; } = "text";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
}

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