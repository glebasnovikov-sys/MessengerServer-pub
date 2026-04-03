using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessengerServer.Data;
using MessengerServer.Models;
using System.Security.Cryptography;
using System.Text;

namespace MessengerServer.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db) => _db = db;

    [HttpGet("check-tag/{tag}")]
    public async Task<IActionResult> CheckTag(string tag)
    {
        var clean = tag.TrimStart('@').ToLowerInvariant();
        var taken = await _db.Users.AnyAsync(u => u.Tag == clean);
        return Ok(new { taken });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var clean = req.Tag.TrimStart('@').ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Tag == clean))
            return BadRequest(new { error = "Тег уже занят" });

        var user = new User
        {
            Tag = clean,
            DisplayName = req.DisplayName,
            PasswordHash = HashPassword(req.Password),
            Bio = req.Bio,
            AvatarColor = GetRandomColor(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(ToDto(user));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var clean = req.Tag.TrimStart('@').ToLowerInvariant();
        var hash = HashPassword(req.Password);
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Tag == clean && u.PasswordHash == hash);
        if (user == null)
            return Unauthorized(new { error = "Неверный тег или пароль" });
        return Ok(ToDto(user));
    }

    [HttpGet("user/{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        return Ok(ToDto(user));
    }

    [HttpGet("search/{query}/{excludeId}")]
    public async Task<IActionResult> Search(string query, int excludeId)
    {
        var q = query.TrimStart('@').ToLowerInvariant();
        var users = await _db.Users
            .Where(u => u.Tag.Contains(q) && u.Id != excludeId)
            .Take(20)
            .ToListAsync();
        return Ok(users.Select(ToDto));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var user = await _db.Users.FindAsync(req.UserId);
        if (user == null) return NotFound();
        user.DisplayName = req.DisplayName;
        user.Bio = req.Bio;
        await _db.SaveChangesAsync();
        return Ok(ToDto(user));
    }

    [HttpPost("avatar/{userId}")]
    public async Task<IActionResult> UploadAvatar(int userId, IFormFile file)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "avatars");
        Directory.CreateDirectory(uploadsDir);

        // Удаляем старый файл
        if (!string.IsNullOrEmpty(user.AvatarColor))
        {
            var oldFileName = Path.GetFileName(user.AvatarColor.Split('?')[0]);
            var oldPath = Path.Combine(uploadsDir, oldFileName);
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fileName = $"{userId}_{ts}.jpg";
        var filePath = Path.Combine(uploadsDir, fileName);

        using var stream = System.IO.File.OpenWrite(filePath);
        await file.CopyToAsync(stream);

        user.AvatarColor = $"/avatars/{fileName}";
        await _db.SaveChangesAsync();
        return Ok(ToDto(user));
    }

    [HttpPost("fcm-token")]
    public async Task<IActionResult> SaveFcmToken([FromBody] FcmTokenRequest req)
    {
        Console.WriteLine($"[FCM-SERVER] Получен токен для userId={req.UserId}");
        Console.WriteLine($"[FCM-SERVER] Токен: {req.Token?[..Math.Min(20, req.Token?.Length ?? 0)]}...");

        var user = await _db.Users.FindAsync(req.UserId);
        if (user == null)
        {
            Console.WriteLine($"[FCM-SERVER] ❌ Пользователь {req.UserId} не найден!");
            return NotFound();
        }

        user.FcmToken = req.Token;
        await _db.SaveChangesAsync();

        Console.WriteLine($"[FCM-SERVER] ✅ Токен сохранён для {user.DisplayName} (id={user.Id})");
        return Ok();
    }

    [HttpGet("status/{userId}")]
    public async Task<IActionResult> GetStatus(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        // ✅ Возвращаем и IsOnline и LastSeen
        return Ok(new
        {
            lastSeen = user.LastSeen,
            isOnline = user.IsOnline
        });
    }

    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Tag = u.Tag,
        DisplayName = u.DisplayName,
        Bio = u.Bio,
        AvatarColor = u.AvatarColor
    };

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password + "messenger_salt_v1"));
        return Convert.ToHexString(bytes);
    }

    private static readonly string[] _colors =
    [
        "#5B4FCF", "#E05C97", "#2ECC8F", "#E8913A",
        "#3AACE8", "#C05CE8", "#26A69A", "#EF5350",
        "#AB47BC", "#42A5F5", "#FF7043", "#66BB6A"
    ];

    private static string GetRandomColor()
    {
        var rng = new Random(Guid.NewGuid().GetHashCode());
        return _colors[rng.Next(_colors.Length)];
    }
}