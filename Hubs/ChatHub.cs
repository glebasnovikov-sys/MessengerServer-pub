using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MessengerServer.Data;

namespace MessengerServer.Hubs;

public class ChatHub : Hub
{
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int>
        _connections = new();

    public ChatHub(AppDbContext db, IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _scopeFactory = scopeFactory;
    }

    public async Task JoinUser(int userId)
    {
        _connections[Context.ConnectionId] = userId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastSeen = DateTime.UtcNow;
            user.IsOnline = true;
            await _db.SaveChangesAsync();
        }

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
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemove(Context.ConnectionId, out var userId))
        {
            var stillConnected = _connections.Values.Any(v => v == userId);

            if (!stillConnected)
            {
                // Захватываем клиентов ДО входа в фоновый Task
                var clients = Clients;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);

                    // Перепроверяем после задержки
                    var stillOnline = _connections.Values.Any(v => v == userId);
                    if (stillOnline) return;

                    // ✅ Новый scope — свой DbContext, не disposed
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    try
                    {
                        await db.Users
                            .Where(u => u.Id == userId)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(u => u.IsOnline, false)
                                .SetProperty(u => u.LastSeen, DateTime.UtcNow));

                        await clients.Others.SendAsync("UserOffline", userId);
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