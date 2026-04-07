using Microsoft.EntityFrameworkCore;
using MessengerServer.Models;

namespace MessengerServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<DeletedChat> DeletedChats => Set<DeletedChat>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupMessage> GroupMessages => Set<GroupMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Tag).IsUnique();

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.User)
            .WithMany()
            .HasForeignKey(gm => gm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupMessage>()
            .HasOne(gm => gm.Group)
            .WithMany(g => g.Messages)
            .HasForeignKey(gm => gm.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupMessage>()
            .HasOne(gm => gm.Sender)
            .WithMany()
            .HasForeignKey(gm => gm.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}