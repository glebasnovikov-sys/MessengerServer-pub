using Microsoft.AspNetCore.SignalR;
using MessengerServer.Data;

namespace MessengerServer.Hubs;

public class ChatHub : Hub
{
    private readonly AppDbContext _db;

    // ✅ ConcurrentDictionary — потокобезопасен
    // connectionId → userId
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int>
        _connections = new();

    public ChatHub(AppDbContext db) => _db = db;

    public async Task JoinUser(int userId)
    {
        _connections[Context.ConnectionId] = userId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastSeen = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // ✅ Шлём UserOnline только если это ПЕРВОЕ подключение этого юзера
        // (не дубликат от реконнекта)
        var connectionsForUser = _connections.Values.Count(v => v == userId);
        if (connectionsForUser == 1)
            await Clients.Others.SendAsync("UserOnline", userId);
    }

    public async Task StartTyping(int toUserId)
    {
        if (!_connections.TryGetValue(Context.ConnectionId, out var fromUserId))
            return;
        await Clients.Group($"user_{toUserId}")
            .SendAsync("UserTyping", fromUserId);
    }

    public async Task StopTyping(int toUserId)
    {
        if (!_connections.TryGetValue(Context.ConnectionId, out var fromUserId))
            return;
        await Clients.Group($"user_{toUserId}")
            .SendAsync("UserStoppedTyping", fromUserId);
    }

    // ✅ Пинг — только обновляет LastSeen, НЕ рассылает UserOnline спам
    public async Task Ping(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;
        user.LastSeen = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        // ✅ Убрали Clients.Others.SendAsync("UserOnline") отсюда —
        // статус онлайн определяется через LastSeen на клиенте
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemove(Context.ConnectionId, out var userId))
        {
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastSeen = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            // ✅ Шлём UserOffline только если у юзера НЕТ других активных подключений
            // (защита от false-offline при реконнекте)
            var stillConnected = _connections.Values.Any(v => v == userId);
            if (!stillConnected)
            {
                // ✅ Небольшая задержка — даём время реконнекту подняться
                // прежде чем объявлять оффлайн
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000); // 3 секунды
                    var stillOnline = _connections.Values.Any(v => v == userId);
                    if (!stillOnline)
                    {
                        await Clients.Others.SendAsync("UserOffline", userId);
                    }
                });
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}