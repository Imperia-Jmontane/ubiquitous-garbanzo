using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyApp.Domain.Identity;
using MyApp.Domain.Observability;

namespace MyApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserExternalLogin> UserExternalLogins { get; set; } = null!;

        public DbSet<GitHubOAuthState> GitHubOAuthStates { get; set; } = null!;

        public DbSet<AuditTrailEntry> AuditTrailEntries { get; set; } = null!;

        public DbSet<FlowBranchPreference> FlowBranchPreferences { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            if (Database.IsSqlite())
            {
                ValueConverter<DateTimeOffset, DateTime> dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, DateTime>(
                    value => value.UtcDateTime,
                    value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

                modelBuilder.Entity<UserExternalLogin>(entity =>
                {
                    entity.Property(login => login.CreatedAt).HasConversion(dateTimeOffsetConverter);
                    entity.Property(login => login.UpdatedAt).HasConversion(dateTimeOffsetConverter);
                    entity.Property(login => login.ExpiresAt).HasConversion(dateTimeOffsetConverter);
                });

                modelBuilder.Entity<GitHubOAuthState>(entity =>
                {
                    entity.Property(state => state.CreatedAt).HasConversion(dateTimeOffsetConverter);
                    entity.Property(state => state.ExpiresAt).HasConversion(dateTimeOffsetConverter);
                });

                modelBuilder.Entity<AuditTrailEntry>(entity =>
                {
                    entity.Property(entry => entry.OccurredAt).HasConversion(dateTimeOffsetConverter);
                });

                modelBuilder.Entity<FlowBranchPreference>(entity =>
                {
                    entity.Property(preference => preference.UpdatedAt).HasConversion(dateTimeOffsetConverter);
                });
            }

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

            modelBuilder.Entity<GitHubOAuthState>(entity =>
            {
                entity.ToTable("GitHubOAuthStates");
                entity.HasKey(state => state.Id);
                entity.Property(state => state.UserId)
                    .IsRequired();
                entity.Property(state => state.State)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(state => state.RedirectUri)
                    .IsRequired()
                    .HasMaxLength(500);
                entity.Property(state => state.CreatedAt)
                    .IsRequired();
                entity.Property(state => state.ExpiresAt)
                    .IsRequired();
                entity.HasIndex(state => state.State)
                    .IsUnique();
            });

            modelBuilder.Entity<AuditTrailEntry>(entity =>
            {
                entity.ToTable("AuditTrailEntries");
                entity.HasKey(entry => entry.Id);
                entity.Property(entry => entry.UserId)
                    .IsRequired();
                entity.Property(entry => entry.EventType)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(entry => entry.Provider)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(entry => entry.Payload)
                    .IsRequired();
                entity.Property(entry => entry.OccurredAt)
                    .IsRequired();
                entity.Property(entry => entry.CorrelationId)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.HasIndex(entry => new { entry.UserId, entry.EventType, entry.OccurredAt });
            });

            modelBuilder.Entity<FlowBranchPreference>(entity =>
            {
                entity.ToTable("FlowBranchPreferences");
                entity.HasKey(preference => preference.UserId);
                entity.Property(preference => preference.CreateLinkedBranches)
                    .IsRequired();
                entity.Property(preference => preference.UpdatedAt)
                    .IsRequired();
            });
        }
    }
}
