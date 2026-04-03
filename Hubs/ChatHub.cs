using Microsoft.AspNetCore.SignalR;
using MessengerServer.Data;

namespace MessengerServer.Hubs;

public class ChatHub : Hub
{
    private readonly AppDbContext _db;
    // connectionId → userId
    private static readonly Dictionary<string, int> _connections = new();

    public ChatHub(AppDbContext db) => _db = db;

    public async Task JoinUser(int userId)
    {
        _connections[Context.ConnectionId] = userId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        // Обновляем LastSeen и рассылаем онлайн-статус
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastSeen = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        await Clients.Others.SendAsync("UserOnline", userId);
    }

    // Клиент вызывает когда пользователь начинает печатать
    public async Task StartTyping(int toUserId)
    {
        if (!_connections.TryGetValue(Context.ConnectionId, out var fromUserId))
            return;
        await Clients.Group($"user_{toUserId}")
            .SendAsync("UserTyping", fromUserId);
    }

    // Клиент вызывает когда перестал печатать
    public async Task StopTyping(int toUserId)
    {
        if (!_connections.TryGetValue(Context.ConnectionId, out var fromUserId))
            return;
        await Clients.Group($"user_{toUserId}")
            .SendAsync("UserStoppedTyping", fromUserId);
    }

    // Пинг — обновляет LastSeen (клиент шлёт каждые 30с)
    public async Task Ping(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;
        user.LastSeen = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await Clients.Others.SendAsync("UserOnline", userId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryGetValue(Context.ConnectionId, out var userId))
        {
            _connections.Remove(Context.ConnectionId);

            // Обновляем LastSeen при отключении
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastSeen = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            await Clients.Others.SendAsync("UserOffline", userId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}