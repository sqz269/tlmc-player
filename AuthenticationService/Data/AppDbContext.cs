using AuthenticationService.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationService.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Role> Roles { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.Entity<User>()
        //    .HasMany(user => user.Tokens)
        //    .WithOne(token => token.User)
        //    .HasForeignKey(token => token.UserId);

        //modelBuilder.Entity<RefreshToken>()
        //    .HasOne(token => token.User)
        //    .WithMany(user => user.Tokens)
        //    .HasForeignKey(token => token.UserId);

        base.OnModelCreating(modelBuilder);
    }
}