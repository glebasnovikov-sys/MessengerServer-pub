using Microsoft.EntityFrameworkCore;
using MessengerServer.Data;
using MessengerServer.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Railway даёт DATABASE_URL в формате postgresql://user:pass@host:port/db
// Npgsql нужен формат Host=...;Database=...
var rawUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string? connectionString;

if (!string.IsNullOrEmpty(rawUrl))
{
    var uri = new Uri(rawUrl);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}
else
{
    connectionString = builder.Configuration.GetConnectionString("Default");
}

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
Directory.CreateDirectory(Path.Combine(wwwroot, "avatars"));

app.UseCors();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwroot),
    RequestPath = ""
});

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

var urls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "http://localhost:5000";
Console.WriteLine($"\n✅ Сервер запущен: {urls}");
Console.WriteLine("📱 Для телефона в той же WiFi используй IP своего ПК\n");

app.Run("http://0.0.0.0:5000");