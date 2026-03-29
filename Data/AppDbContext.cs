using Microsoft.EntityFrameworkCore;
using MessengerServer.Models;

namespace MessengerServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Message> Messages => Set<Message>();

    public DbSet<DeletedChat> DeletedChats { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Tag).IsUnique();
    }
}
