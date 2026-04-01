using Microsoft.EntityFrameworkCore;

namespace Core.Database;

public class Context : DbContext
{
    public DbSet<Models.Session> Sessions { get; set; }
    public DbSet<Models.Application> Applications { get; set; }
    public DbSet<Models.User> Users { get; set; }
    public DbSet<Models.Friendship> Friendships { get; set; }
    
    public Context(DbContextOptions<Context> options) : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Models.User>(entity =>
        {
            entity.HasIndex(e => e.Username);
            entity.HasIndex(e => e.UserCode).IsUnique();
            entity.HasIndex(e => e.KeyFingerprint);
        });
        
        modelBuilder.Entity<Models.Friendship>(entity =>
        {
            entity.HasOne(f => f.User)
                .WithMany(u => u.InitiatedFriendships)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(f => f.Friend)
                .WithMany(u => u.ReceivedFriendships)
                .HasForeignKey(f => f.FriendId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(e => new { e.UserId, e.FriendId }).IsUnique();
            entity.HasIndex(e => e.Status);
        });
    }
}