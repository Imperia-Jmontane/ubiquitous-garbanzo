using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Identity;

namespace MyApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserExternalLogin> UserExternalLogins { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserExternalLogin>(entity =>
            {
                entity.ToTable("UserExternalLogins");
                entity.HasKey(login => login.Id);
                entity.Property(login => login.Provider)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(login => login.ExternalUserId)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(login => login.AccessToken)
                    .IsRequired()
                    .HasMaxLength(4000);
                entity.Property(login => login.RefreshToken)
                    .IsRequired()
                    .HasMaxLength(4000);
                entity.Property(login => login.CreatedAt)
                    .IsRequired();
                entity.Property(login => login.UpdatedAt)
                    .IsRequired();
                entity.Property(login => login.UserId)
                    .IsRequired();
                entity.HasIndex(login => new { login.UserId, login.Provider })
                    .IsUnique();
            });
        }
    }
}
