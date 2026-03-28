using Microsoft.EntityFrameworkCore;
using MessengerServer.Data;
using MessengerServer.Hubs;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSignalR();

var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ✅ Инициализация Firebase
var firebaseKeyPath = Path.Combine(
    AppContext.BaseDirectory, "firebase-key.json");

// При запуске из Visual Studio ищем рядом с .csproj
if (!File.Exists(firebaseKeyPath))
    firebaseKeyPath = Path.Combine(
        Directory.GetCurrentDirectory(), "firebase-key.json");

if (File.Exists(firebaseKeyPath))
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.FromFile(firebaseKeyPath)
    });
    Console.WriteLine("✅ Firebase инициализирован");
}
else
{
    Console.WriteLine($"⚠️ firebase-key.json не найден: {firebaseKeyPath}");
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

Console.WriteLine("🚀 Сервер запущен на http://0.0.0.0:5000");
app.Run("http://0.0.0.0:5000");