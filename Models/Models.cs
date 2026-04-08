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

// ─── ГРУППЫ ──────────────────────────────────────────────────────────────────

public class Group
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AvatarColor { get; set; }
    public string? AvatarUrl { get; set; }  // ← ДОБАВЛЕНО
    public int OwnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<GroupMember> Members { get; set; } = [];
    public List<GroupMessage> Messages { get; set; } = [];
}

public class GroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public Group? Group { get; set; }
    public User? User { get; set; }
}

public class GroupMessage
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int SenderId { get; set; }
    [Required] public string Text { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string MessageType { get; set; } = "text";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public Group? Group { get; set; }
    public User? Sender { get; set; }
}

// ��── ТАБЛИЦА ДЛЯ ПРОЧТЕНИЯ СООБЩЕНИЙ В ГРУППАХ ─────────────────────────────
// ← ДОБАВЛЕНО
public class GroupMessageRead
{
    public int Id { get; set; }
    public int GroupMessageId { get; set; }
    public int UserId { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}

// ─── DTO ─────────────────────────────────────────────────────────────────────

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

public class CreateGroupRequest
{
    public int OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<int> MemberIds { get; set; } = [];
}

public class SendGroupMessageRequest
{
    public int SenderId { get; set; }
    public int GroupId { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class GroupMessageDto
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string MessageType { get; set; } = "text";
    public DateTime SentAt { get; set; }
    public int ReadCount { get; set; }  // ← ДОБАВЛЕНО
}

public class GroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AvatarColor { get; set; }
    public string? AvatarUrl { get; set; }  // ← ДОБАВЛЕНО
    public int OwnerId { get; set; }
    public int MemberCount { get; set; }
    public int OnlineCount { get; set; }
    public List<UserDto> Members { get; set; } = [];
}