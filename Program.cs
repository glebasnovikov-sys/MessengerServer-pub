using Microsoft.EntityFrameworkCore;
using MessengerServer.Data;
using MessengerServer.Hubs;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSignalR();

// ✅ Строка подключения — из переменной окружения Railway или из appsettings.json
var connectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL") // Railway даёт в формате postgres://...
    ?? builder.Configuration.GetConnectionString("Default");

// Railway даёт DATABASE_URL в формате postgres://user:pass@host:port/db
// Npgsql понимает только Host=...;Port=... формат — конвертируем
if (connectionString != null && connectionString.StartsWith("postgres://"))
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ✅ Инициализация Firebase — ключ из переменной окружения
var firebaseJson = Environment.GetEnvironmentVariable("FIREBASE_KEY_JSON");
if (!string.IsNullOrEmpty(firebaseJson))
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.FromJson(firebaseJson)
    });
    Console.WriteLine("✅ Firebase инициализирован из переменной окружения");
}
else
{
    // Локальная разработка — из файла
    var firebaseKeyPath = Path.Combine(AppContext.BaseDirectory, "firebase-key.json");
    if (!File.Exists(firebaseKeyPath))
        firebaseKeyPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-key.json");

    if (File.Exists(firebaseKeyPath))
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromFile(firebaseKeyPath)
        });
        Console.WriteLine("✅ Firebase инициализирован из файла");
    }
    else
    {
        Console.WriteLine("⚠️ firebase-key.json не найден и FIREBASE_KEY_JSON не задан");
    }
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
Directory.CreateDirectory(Path.Combine(wwwroot, "avatars"));
Directory.CreateDirectory(Path.Combine(wwwroot, "audio"));
Directory.CreateDirectory(Path.Combine(wwwroot, "video"));

app.UseCors();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwroot),
    RequestPath = ""
});

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

// ✅ Порт из переменной окружения Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"🚀 Сервер запущен на порту {port}");
app.Run($"http://0.0.0.0:{port}");