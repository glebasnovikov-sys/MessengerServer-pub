using Microsoft.AspNetCore.SignalR;
using MessengerServer.Data;

namespace MessengerServer.Hubs;

public class ChatHub : Hub
{
    private readonly AppDbContext _db;

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
            // ✅ Явно помечаем онлайн
            user.IsOnline = true;
            await _db.SaveChangesAsync();
        }

        // ✅ Шлём только если это первое подключение этого юзера
        var count = _connections.Values.Count(v => v == userId);
        if (count == 1)
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

    public async Task Ping(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;
        user.LastSeen = DateTime.UtcNow;
        user.IsOnline = true;
        await _db.SaveChangesAsync();
        // ✅ НЕ рассылаем UserOnline спам — статус читается через GetStatus
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemove(Context.ConnectionId, out var userId))
        {
            // ✅ Проверяем есть ли ещё подключения этого юзера
            var stillConnected = _connections.Values.Any(v => v == userId);

            if (!stillConnected)
            {
                // ✅ Задержка 3 секунды — даём реконнекту подняться
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);

                    // Перепроверяем после задержки
                    var stillOnline = _connections.Values.Any(v => v == userId);
                    if (stillOnline) return; // реконнектнулся — не трогаем

                    // ✅ Явно помечаем оффлайн в БД
                    using var scope = _db.Database.GetDbConnection()
                        .CreateCommand().Connection is not null
                        ? null
                        : null; // не используем scope — используем новый контекст

                    // Обновляем через прямой SQL чтобы не держать контекст
                    try
                    {
                        await _db.Users
                            .Where(u => u.Id == userId)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(u => u.IsOnline, false)
                                .SetProperty(u => u.LastSeen, DateTime.UtcNow));

                        await Clients.Others.SendAsync("UserOffline", userId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Hub] Offline update error: {ex.Message}");
                    }
                });
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}