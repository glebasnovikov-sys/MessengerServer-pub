using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MessengerServer.Data;
using MessengerServer.Hubs;
using MessengerServer.Models;

namespace MessengerServer.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public MessagesController(AppDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // ─── GET переписка с пагинацией ──────────────────────────────────────────
    [HttpGet("{userId}/{otherId}")]
    public async Task<IActionResult> GetConversation(
        int userId, int otherId,
        [FromQuery] int beforeId = 0,
        [FromQuery] int take = 30)
    {
        // Проверяем дату удаления чата
        var deletedChat = await _db.DeletedChats
            .FirstOrDefaultAsync(d => d.UserId == userId && d.PeerId == otherId);

        var query = _db.Messages
            .Where(m =>
                (m.SenderId == userId && m.ReceiverId == otherId) ||
                (m.SenderId == otherId && m.ReceiverId == userId));

        // Скрываем сообщения до даты удаления
        if (deletedChat != null)
            query = query.Where(m => m.SentAt > deletedChat.DeletedAt);

        List<Message> msgs;
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

        return Ok(new { messages = msgs, hasMore });
    }
  

    // ─── Пометить прочитанными ───────────────────────────────────────────────
    [HttpPost("read/{userId}/{otherId}")]
    public async Task<IActionResult> MarkRead(int userId, int otherId)
    {
        var unread = await _db.Messages
            .Where(m => m.SenderId == otherId
                     && m.ReceiverId == userId
                     && !m.IsRead)
            .ToListAsync();

        if (unread.Count > 0)
        {
            unread.ForEach(m => m.IsRead = true);
            await _db.SaveChangesAsync();

            foreach (var m in unread)
            {
                await _hub.Clients
                    .Group($"user_{m.SenderId}")
                    .SendAsync("MessageRead", m.Id);
            }
        }

        return Ok();
    }

    // ─── Новые входящие после lastId ─────────────────────────────────────────
    [HttpGet("new/{userId}/{otherId}/{lastId}")]
    public async Task<IActionResult> GetNewIncoming(
        int userId, int otherId, int lastId)
    {
        var msgs = await _db.Messages
            .Where(m => m.Id > lastId
                     && m.SenderId == otherId
                     && m.ReceiverId == userId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        return Ok(msgs);
    }

    // ─── Список чатов ────────────────────────────────────────────────────────
    [HttpGet("chats/{userId}")]
    public async Task<IActionResult> GetChats(int userId)
    {
        // Загружаем записи об удалённых чатах
        var deletedChats = await _db.DeletedChats
            .Where(d => d.UserId == userId)
            .ToDictionaryAsync(d => d.PeerId, d => d.DeletedAt);

        var allMsgs = await _db.Messages
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        var seen = new HashSet<int>();
        var previews = new List<object>();

        foreach (var msg in allMsgs)
        {
            var otherId = msg.SenderId == userId
                ? msg.ReceiverId : msg.SenderId;

            if (!seen.Add(otherId)) continue;

            // Скрываем чат если удалён и новых сообщений после удаления нет
            if (deletedChats.TryGetValue(otherId, out var deletedAt)
                && msg.SentAt <= deletedAt)
                continue;

            var other = await _db.Users.FindAsync(otherId);
            if (other == null) continue;

            var unread = allMsgs.Count(m =>
                m.SenderId == otherId
             && m.ReceiverId == userId
             && !m.IsRead
             && (!deletedChats.ContainsKey(otherId) || m.SentAt > deletedChats[otherId]));

            previews.Add(new
            {
                UserId = otherId,
                other.Tag,
                other.DisplayName,
                other.AvatarColor,
                LastMessage = msg.MessageType == "audio" ? "🎤 Голосовое сообщение"
                            : msg.MessageType == "video" ? "📹 Видеосообщение"
                            : msg.Text,
                LastTime = msg.SentAt,
                UnreadCount = unread
            });
        }

        return Ok(previews);
    }

    // ─── Отправить текст ─────────────────────────────────────────────────────
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest req)
    {
        var msg = new Message
        {
            SenderId = req.SenderId,
            ReceiverId = req.ReceiverId,
            Text = req.Text,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();

        await _hub.Clients
            .Group($"user_{req.ReceiverId}")
            .SendAsync("NewMessage", msg);

        var receiver = await _db.Users.FindAsync(req.ReceiverId);
        var sender = await _db.Users.FindAsync(req.SenderId);

        if (receiver?.FcmToken != null && sender != null)
            await SendPushAsync(receiver.FcmToken, sender.DisplayName, req.Text);
        else
            Console.WriteLine($"[FCM] Пропуск пуша: receiver.FcmToken={receiver?.FcmToken ?? "NULL"}");

        return Ok(msg);
    }

    // ─── Отправить аудио ─────────────────────────────────────────────────────
    [HttpPost("audio")]
    public async Task<IActionResult> SendAudio(
        [FromForm] int senderId,
        [FromForm] int receiverId,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file");

        var ext = Path.GetExtension(file.FileName).ToLower();
        var allowed = new[] { ".mp3", ".m4a", ".aac", ".ogg", ".wav", ".amr" };
        if (!allowed.Contains(ext)) ext = ".m4a";

        var fileName = $"{Guid.NewGuid()}{ext}";
        var audioDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "audio");
        Directory.CreateDirectory(audioDir);
        var filePath = Path.Combine(audioDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var msg = new Message
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Text = "",
            AudioUrl = $"/audio/{fileName}",
            MessageType = "audio",
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();

        await _hub.Clients
            .Group($"user_{receiverId}")
            .SendAsync("NewMessage", msg);

        var receiver = await _db.Users.FindAsync(receiverId);
        var sender = await _db.Users.FindAsync(senderId);

        if (receiver?.FcmToken != null && sender != null)
            await SendPushAsync(receiver.FcmToken, sender.DisplayName, "🎤 Голосовое сообщение");
        else
            Console.WriteLine($"[FCM] Пропуск пуша: receiver.FcmToken={receiver?.FcmToken ?? "NULL"}");

        return Ok(msg);
    }

    // ─── Удалить чат ─────────────────────────────────────────────────────────
    [HttpPost("delete/{userId}/{peerId}")]
    public async Task<IActionResult> DeleteChat(int userId, int peerId)
    {
        // Удаляем старую запись если есть
        var existing = await _db.DeletedChats
            .FirstOrDefaultAsync(d => d.UserId == userId && d.PeerId == peerId);

        if (existing != null)
            existing.DeletedAt = DateTime.UtcNow;
        else
            _db.DeletedChats.Add(new DeletedChat
            {
                UserId = userId,
                PeerId = peerId,
                DeletedAt = DateTime.UtcNow
            });

        await _db.SaveChangesAsync();
        return Ok();
    }

    // ─── Отправить видео ─────────────────────────────────────────────────────
    [HttpPost("video")]
    public async Task<IActionResult> SendVideo(
        [FromForm] int senderId,
        [FromForm] int receiverId,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file");

        var fileName = $"{Guid.NewGuid()}.mp4";
        var videoDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "video");
        Directory.CreateDirectory(videoDir);
        var filePath = Path.Combine(videoDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var msg = new Message
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Text = "",
            VideoUrl = $"/video/{fileName}",
            MessageType = "video",
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();

        await _hub.Clients
            .Group($"user_{receiverId}")
            .SendAsync("NewMessage", msg);

        var receiver = await _db.Users.FindAsync(receiverId);
        var sender = await _db.Users.FindAsync(senderId);

        if (receiver?.FcmToken != null && sender != null)
            await SendPushAsync(receiver.FcmToken, sender.DisplayName, "📹 Видеосообщение");
        else
            Console.WriteLine($"[FCM] Пропуск пуша: receiver.FcmToken={receiver?.FcmToken ?? "NULL"}");

        return Ok(msg);
    }

    // ─── FCM ─────────────────────────────────────────────────────────────────
    private static async Task SendPushAsync(
        string fcmToken, string senderName, string messageText)
    {
        try
        {
            Console.WriteLine($"[FCM] Отправляем пуш → {senderName}: {messageText}");
            Console.WriteLine($"[FCM] Токен: {fcmToken[..Math.Min(20, fcmToken.Length)]}...");

            var message = new FirebaseAdmin.Messaging.Message
            {
                Token = fcmToken,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = senderName,
                    Body = messageText
                },
                Android = new FirebaseAdmin.Messaging.AndroidConfig
                {
                    Priority = FirebaseAdmin.Messaging.Priority.High,
                    Notification = new FirebaseAdmin.Messaging.AndroidNotification
                    {
                        Sound = "default",
                        ChannelId = "messages"
                    }
                }
            };

            var result = await FirebaseAdmin.Messaging.FirebaseMessaging
                .DefaultInstance.SendAsync(message);

            Console.WriteLine($"[FCM] ✅ Пуш отправлен успешно! MessageId: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FCM] ❌ Ошибка отправки: {ex.Message}");
        }
    }
}