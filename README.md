# MessengerServer

ASP.NET Core сервер для мессенджера.

## Запуск

```bash
cd MessengerServer
dotnet run
```

Сервер стартует на http://0.0.0.0:5000

## Для телефона в той же WiFi

1. Узнай IP своего ПК:
```powershell
ipconfig
# Ищи IPv4 Address — например 192.168.1.5
```

2. В `MessengerApp/Services/ApiService.cs` замени:
```csharp
public static string BaseUrl { get; set; } = "http://localhost:5000";
// на:
public static string BaseUrl { get; set; } = "http://192.168.1.5:5000";
```

3. Пересобери APK и установи на телефон.

## API endpoints

- POST /api/auth/register
- POST /api/auth/login
- GET  /api/auth/check-tag/{tag}
- GET  /api/auth/user/{id}
- GET  /api/auth/search/{query}/{excludeId}
- PUT  /api/auth/profile
- GET  /api/messages/{userId}/{otherId}
- GET  /api/messages/new/{userId}/{otherId}/{lastId}
- GET  /api/messages/chats/{userId}
- POST /api/messages/send
- WS   /hubs/chat  (SignalR)
