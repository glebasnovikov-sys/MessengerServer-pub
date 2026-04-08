namespace MessApp.Models;

public class UserDto
{
    public int Id { get; set; }
    public string Tag { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }
    public string? AvatarColor { get; set; }
    public string? FcmToken { get; set; }
    public DateTime LastSeen { get; set; }
}

public class Message
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string Text { get; set; } = "";
    public string? AudioUrl { get; set; }
    public string MessageType { get; set; } = "text";
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public string? VideoUrl { get; set; }
}

public class ChatPreview
{
    public int UserId { get; set; }
    public string Tag { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarColor { get; set; }
    public string? LastMessage { get; set; }
    public DateTime LastTime { get; set; }
    public int UnreadCount { get; set; }
}

// ─── ГРУППЫ ──────────────────────────────────────────────────────────────────

public class GroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? AvatarColor { get; set; }
    public string? AvatarUrl { get; set; }
    public int OwnerId { get; set; }
    public int MemberCount { get; set; }
    public int OnlineCount { get; set; }
    public List<UserDto> Members { get; set; } = [];

    // ← ДОБАВЬТЕ ЭТИ ДВА СВОЙСТВА
    public string? LastMessageText { get; set; }
    public DateTime LastMessageAt { get; set; }
}

public class GroupMessageDto
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public string? SenderAvatar { get; set; }
    public string Text { get; set; } = "";
    public string? AudioUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string MessageType { get; set; } = "text";
    public DateTime SentAt { get; set; }
    public int ReadCount { get; set; }
}

public class GroupPreview
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? AvatarColor { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LastMessage { get; set; }
    public DateTime LastTime { get; set; }
    public int MemberCount { get; set; }
    public int OnlineCount { get; set; }
    public bool IsGroup => true;
}

// ─── ЗАПРОСЫ ─────────────────────────────────────────────────────────────────

public class RegisterRequest
{
    public string Tag { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Bio { get; set; }
}

public class SendMessageRequest
{
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string Text { get; set; } = "";
}

public class UpdateProfileRequest
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }
}

public class CreateGroupRequest
{
    public int OwnerId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<int> MemberIds { get; set; } = [];
}

public class SendGroupMessageRequest
{
    public int SenderId { get; set; }
    public int GroupId { get; set; }
    public string Text { get; set; } = "";
}