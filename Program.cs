using Microsoft.EntityFrameworkCore;
using MessengerServer.Data;
using MessengerServer.Hubs;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSignalR();

// ✅ Строка подключения из переменной окружения Railway
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("Default");

// Конвертируем postgres:// или postgresql:// в формат Npgsql
if (connectionString != null &&
    (connectionString.StartsWith("postgres://") ||
     connectionString.StartsWith("postgresql://")))
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');
    var user = Uri.UnescapeDataString(userInfo[0]);
    var pass = Uri.UnescapeDataString(userInfo[1]);
    var db = uri.AbsolutePath.TrimStart('/');
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
    Console.WriteLine($"✅ Строка подключения сконвертирована для {uri.Host}");
}

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ✅ Firebase из переменной окружения
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
        Console.WriteLine("⚠️ Firebase не инициализирован");
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

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"🚀 Сервер запущен на порту {port}");
app.Run($"http://0.0.0.0:{port}");