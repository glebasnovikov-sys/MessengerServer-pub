using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MessengerServer.Data;
using MessengerServer.Hubs;
using MessengerServer.Models;

namespace MessengerServer.Controllers;

[ApiController]
[Route("api/groups")]
public class GroupsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public GroupsController(AppDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // Создать группу
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest req)
    {
        var group = new Group
        {
            Name = req.Name,
            Description = req.Description,
            OwnerId = req.OwnerId,
            AvatarColor = GetRandomColor(),
            CreatedAt = DateTime.UtcNow
        };
        _db.Groups.Add(group);
        await _db.SaveChangesAsync();

        // Добавляем создателя
        var allIds = req.MemberIds.Contains(req.OwnerId)
            ? req.MemberIds
            : req.MemberIds.Append(req.OwnerId).ToList();

        foreach (var uid in allIds.Distinct())
        {
            _db.GroupMembers.Add(new GroupMember
            {
                GroupId = group.Id,
                UserId = uid,
                JoinedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        // Уведомляем всех участников через SignalR
        foreach (var uid in allIds.Distinct())
        {
            await _hub.Clients
                .Group($"user_{uid}")
                .SendAsync("GroupCreated", group.Id);
        }

        return Ok(await ToDto(group.Id));
    }

    // Получить список групп пользователя
    [HttpGet("list/{userId}")]
    public async Task<IActionResult> GetUserGroups(int userId)
    {
        var groupIds = await _db.GroupMembers
            .Where(gm => gm.UserId == userId)
            .Select(gm => gm.GroupId)
            .ToListAsync();

        var result = new List<object>();
        foreach (var gid in groupIds)
        {
            var dto = await ToDto(gid);
            if (dto != null) result.Add(dto);
        }

        return Ok(result);
    }

    // Получить группу по ID
    [HttpGet("{groupId}")]
    public async Task<IActionResult> GetGroup(int groupId)
    {
        var dto = await ToDto(groupId);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    // Получить сообщения группы (с пагинацией)
    [HttpGet("{groupId}/messages")]
    public async Task<IActionResult> GetMessages(
        int groupId,
        [FromQuery] int beforeId = 0,
        [FromQuery] int take = 30)
    {
        var query = _db.GroupMessages
            .Include(gm => gm.Sender)
            .Where(gm => gm.GroupId == groupId);

        List<GroupMessage> msgs;
        bool hasMore;

        if (beforeId > 0)
        {
            msgs = await query
                .Where(m => m.Id < beforeId)
                .OrderByDescending(m => m.Id)
                .Take(take)
                .OrderBy(m => m.Id)
                .ToListAsync();

            hasMore = await query
                .Where(m => m.Id < (msgs.Count > 0 ? msgs[0].Id : beforeId))
                .AnyAsync();
        }
        else
        {
            msgs = await query
                .OrderByDescending(m => m.Id)
                .Take(take)
                .OrderBy(m => m.Id)
                .ToListAsync();

            hasMore = await query
                .Where(m => m.Id < (msgs.Count > 0 ? msgs[0].Id : int.MaxValue))
                .AnyAsync();
        }

        var dtos = msgs.Select(m => new GroupMessageDto
        {
            Id = m.Id,
            GroupId = m.GroupId,
            SenderId = m.SenderId,
            SenderName = m.Sender?.DisplayName ?? "",
            SenderAvatar = m.Sender?.AvatarColor,
            Text = m.Text,
            AudioUrl = m.AudioUrl,
            VideoUrl = m.VideoUrl,
            MessageType = m.MessageType,
            SentAt = m.SentAt
        });

        return Ok(new { messages = dtos, hasMore });
    }

    // Отправить текстовое сообщение в группу
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendGroupMessageRequest req)
    {
        var isMember = await _db.GroupMembers
            .AnyAsync(gm => gm.GroupId == req.GroupId && gm.UserId == req.SenderId);
        if (!isMember) return Forbid();

        var sender = await _db.Users.FindAsync(req.SenderId);
        if (sender == null) return NotFound();

        var msg = new GroupMessage
        {
            GroupId = req.GroupId,
            SenderId = req.SenderId,
            Text = req.Text,
            MessageType = "text",
            SentAt = DateTime.UtcNow
        };
        _db.GroupMessages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = new GroupMessageDto
        {
            Id = msg.Id,
            GroupId = msg.GroupId,
            SenderId = msg.SenderId,
            SenderName = sender.DisplayName,
            SenderAvatar = sender.AvatarColor,
            Text = msg.Text,
            MessageType = msg.MessageType,
            SentAt = msg.SentAt
        };

        // Уведомляем всех участников группы
        await _hub.Clients
            .Group($"group_{req.GroupId}")
            .SendAsync("NewGroupMessage", dto);

        return Ok(dto);
    }

    // Отправить аудио в группу
    [HttpPost("audio")]
    public async Task<IActionResult> SendAudio(
        [FromForm] int senderId,
        [FromForm] int groupId,
        IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file");

        var isMember = await _db.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == senderId);
        if (!isMember) return Forbid();

        var sender = await _db.Users.FindAsync(senderId);
        if (sender == null) return NotFound();

        var ext = Path.GetExtension(file.FileName).ToLower();
        var allowed = new[] { ".mp3", ".m4a", ".aac", ".ogg", ".wav", ".amr" };
        if (!allowed.Contains(ext)) ext = ".m4a";

        var fileName = $"{Guid.NewGuid()}{ext}";
        var audioDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "audio");
        Directory.CreateDirectory(audioDir);
        var filePath = Path.Combine(audioDir, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var msg = new GroupMessage
        {
            GroupId = groupId,
            SenderId = senderId,
            Text = "",
            AudioUrl = $"/audio/{fileName}",
            MessageType = "audio",
            SentAt = DateTime.UtcNow
        };
        _db.GroupMessages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = new GroupMessageDto
        {
            Id = msg.Id,
            GroupId = msg.GroupId,
            SenderId = msg.SenderId,
            SenderName = sender.DisplayName,
            SenderAvatar = sender.AvatarColor,
            AudioUrl = msg.AudioUrl,
            MessageType = msg.MessageType,
            SentAt = msg.SentAt
        };

        await _hub.Clients
            .Group($"group_{groupId}")
            .SendAsync("NewGroupMessage", dto);

        return Ok(dto);
    }

    // Отправить видео в группу
    [HttpPost("video")]
    public async Task<IActionResult> SendVideo(
        [FromForm] int senderId,
        [FromForm] int groupId,
        IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file");

        var isMember = await _db.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == senderId);
        if (!isMember) return Forbid();

        var sender = await _db.Users.FindAsync(senderId);
        if (sender == null) return NotFound();

        var fileName = $"{Guid.NewGuid()}.mp4";
        var videoDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "video");
        Directory.CreateDirectory(videoDir);
        var filePath = Path.Combine(videoDir, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var msg = new GroupMessage
        {
            GroupId = groupId,
            SenderId = senderId,
            Text = "",
            VideoUrl = $"/video/{fileName}",
            MessageType = "video",
            SentAt = DateTime.UtcNow
        };
        _db.GroupMessages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = new GroupMessageDto
        {
            Id = msg.Id,
            GroupId = msg.GroupId,
            SenderId = msg.SenderId,
            SenderName = sender.DisplayName,
            SenderAvatar = sender.AvatarColor,
            VideoUrl = msg.VideoUrl,
            MessageType = msg.MessageType,
            SentAt = msg.SentAt
        };

        await _hub.Clients
            .Group($"group_{groupId}")
            .SendAsync("NewGroupMessage", dto);

        return Ok(dto);
    }

    // Загрузить аватар группы
    [HttpPost("{groupId}/avatar")]
    public async Task<IActionResult> UploadAvatar(int groupId, IFormFile file)
    {
        var group = await _db.Groups.FindAsync(groupId);
        if (group == null) return NotFound();

        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "avatars");
        Directory.CreateDirectory(uploadsDir);

        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fileName = $"group_{groupId}_{ts}.jpg";
        var filePath = Path.Combine(uploadsDir, fileName);

        using var stream = System.IO.File.OpenWrite(filePath);
        await file.CopyToAsync(stream);

        group.AvatarColor = $"/avatars/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(new { avatarColor = group.AvatarColor });
    }

    // Статус участников группы (количество в сети)
    [HttpGet("{groupId}/status")]
    public async Task<IActionResult> GetGroupStatus(int groupId)
    {
        var members = await _db.GroupMembers
            .Include(gm => gm.User)
            .Where(gm => gm.GroupId == groupId)
            .ToListAsync();

        var total = members.Count;
        var online = members.Count(gm =>
            gm.User != null &&
            gm.User.IsOnline &&
            DateTime.UtcNow - gm.User.LastSeen < TimeSpan.FromSeconds(60));

        return Ok(new { total, online });
    }

    // ─── HELPERS ─────────────────────────────────────────────────────────────
    private async Task<GroupDto?> ToDto(int groupId)
    {
        var group = await _db.Groups
            .Include(g => g.Members)
            .ThenInclude(gm => gm.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null) return null;

        var onlineCount = group.Members.Count(gm =>
            gm.User != null &&
            gm.User.IsOnline &&
            DateTime.UtcNow - gm.User.LastSeen < TimeSpan.FromSeconds(60));

        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            AvatarColor = group.AvatarColor,
            OwnerId = group.OwnerId,
            MemberCount = group.Members.Count,
            OnlineCount = onlineCount,
            Members = group.Members.Select(gm => new UserDto
            {
                Id = gm.User!.Id,
                Tag = gm.User.Tag,
                DisplayName = gm.User.DisplayName,
                AvatarColor = gm.User.AvatarColor
            }).ToList()
        };
    }

    private static readonly string[] _colors =
    [
        "#5B4FCF", "#E05C97", "#2ECC8F", "#E8913A",
        "#3AACE8", "#C05CE8", "#26A69A", "#EF5350"
    ];

    private static string GetRandomColor()
    {
        var rng = new Random(Guid.NewGuid().GetHashCode());
        return _colors[rng.Next(_colors.Length)];
    }
}