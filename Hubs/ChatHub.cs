using Microsoft.AspNetCore.SignalR;

namespace MessengerServer.Hubs;

public class ChatHub : Hub
{
    // Клиент вызывает это чтобы присоединиться к своей личной группе
    public async Task JoinUser(int userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
    }
}
