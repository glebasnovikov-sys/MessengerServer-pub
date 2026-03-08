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

    // Получить переписку и пометить как прочитанные
    [HttpGet("{userId}/{otherId}")]
    public async Task<IActionResult> GetConversation(int userId, int otherId)
    {
        var msgs = await _db.Messages
            .Where(m =>
                (m.SenderId == userId && m.ReceiverId == otherId) ||
                (m.SenderId == otherId && m.ReceiverId == userId))
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        // Пометить входящие как прочитанные
        var unread = msgs.Where(m => m.ReceiverId == userId && !m.IsRead).ToList();
        if (unread.Count > 0)
        {
            unread.ForEach(m => m.IsRead = true);
            await _db.SaveChangesAsync();
        }

        return Ok(msgs);
    }

    // Новые входящие сообщения после lastId
    [HttpGet("new/{userId}/{otherId}/{lastId}")]
    public async Task<IActionResult> GetNewIncoming(int userId, int otherId, int lastId)
    {
        var msgs = await _db.Messages
            .Where(m => m.Id > lastId && m.SenderId == otherId && m.ReceiverId == userId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        if (msgs.Count > 0)
        {
            msgs.ForEach(m => m.IsRead = true);
            await _db.SaveChangesAsync();
        }

        return Ok(msgs);
    }

    // Список чатов (последнее сообщение с каждым)
    [HttpGet("chats/{userId}")]
    public async Task<IActionResult> GetChats(int userId)
    {
        var allMsgs = await _db.Messages
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        var seen = new HashSet<int>();
        var previews = new List<object>();

        foreach (var msg in allMsgs)
        {
            var otherId = msg.SenderId == userId ? msg.ReceiverId : msg.SenderId;
            if (!seen.Add(otherId)) continue;

            var other = await _db.Users.FindAsync(otherId);
            if (other == null) continue;

            var unread = allMsgs.Count(m => m.SenderId == otherId && m.ReceiverId == userId && !m.IsRead);

            previews.Add(new
            {
                UserId = otherId,
                other.Tag,
                other.DisplayName,
                other.AvatarColor,
                LastMessage = msg.Text,
                LastTime = msg.SentAt,
                UnreadCount = unread
            });
        }

        return Ok(previews);
    }

    // Отправить сообщение — сохраняем и пушим через SignalR
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

        // Пушим получателю через SignalR если он онлайн
        await _hub.Clients.Group($"user_{req.ReceiverId}")
            .SendAsync("NewMessage", msg);

        return Ok(msg);
    }
}
