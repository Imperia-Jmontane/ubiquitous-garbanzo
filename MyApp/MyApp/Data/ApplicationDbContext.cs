using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyApp.Domain.CodeAnalysis;
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

        public DbSet<IndexedRepository> IndexedRepositories { get; set; } = null!;

        public DbSet<CodeNode> CodeNodes { get; set; } = null!;

        public DbSet<CodeEdge> CodeEdges { get; set; } = null!;

        public DbSet<SourceFile> SourceFiles { get; set; } = null!;

        public DbSet<SourceLocation> SourceLocations { get; set; } = null!;

        public DbSet<Occurrence> Occurrences { get; set; } = null!;

        public DbSet<IndexingError> IndexingErrors { get; set; } = null!;

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

            ConfigureCodeAnalysisEntities(modelBuilder);
        }

        private static void ConfigureCodeAnalysisEntities(ModelBuilder modelBuilder)
        {
            ConfigureIndexedRepository(modelBuilder);
            ConfigureCodeNode(modelBuilder);
            ConfigureCodeEdge(modelBuilder);
            ConfigureSourceFile(modelBuilder);
            ConfigureSourceLocation(modelBuilder);
            ConfigureOccurrence(modelBuilder);
            ConfigureIndexingError(modelBuilder);
        }

        private static void ConfigureIndexedRepository(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IndexedRepository>(entity =>
            {
                entity.HasKey(repository => repository.Id);

                entity.Property(repository => repository.RepositoryId)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(repository => repository.RepositoryPath)
                    .IsRequired()
                    .HasMaxLength(1000);

                entity.Property(repository => repository.CommitSha)
                    .HasMaxLength(40);

                entity.Property(repository => repository.BranchName)
                    .HasMaxLength(250);

                entity.Property(repository => repository.ErrorMessage)
                    .HasMaxLength(4000);

                entity.HasIndex(repository => repository.RepositoryId)
                    .HasDatabaseName("idx_indexed_repository_id");

                entity.HasIndex(repository => new { repository.RepositoryId, repository.CommitSha })
                    .HasDatabaseName("idx_indexed_repository_commit");

                entity.HasIndex(repository => repository.Status)
                    .HasDatabaseName("idx_indexed_repository_status");

                entity.HasMany(repository => repository.Files)
                    .WithOne(file => file.RepositorySnapshot)
                    .HasForeignKey(file => file.RepositorySnapshotId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(repository => repository.Nodes)
                    .WithOne(node => node.RepositorySnapshot)
                    .HasForeignKey(node => node.RepositorySnapshotId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(repository => repository.Edges)
                    .WithOne(edge => edge.RepositorySnapshot)
                    .HasForeignKey(edge => edge.RepositorySnapshotId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigureCodeNode(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CodeNode>(entity =>
            {
                entity.HasKey(node => node.Id);

                entity.Property(node => node.SerializedName)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.Property(node => node.DisplayName)
                    .HasMaxLength(500);

                entity.Property(node => node.NormalizedName)
                    .HasMaxLength(500);

                entity.HasIndex(node => node.SerializedName)
                    .HasDatabaseName("idx_node_serialized_name");

                entity.HasIndex(node => node.DisplayName)
                    .HasDatabaseName("idx_node_display_name");

                entity.HasIndex(node => node.NormalizedName)
                    .HasDatabaseName("idx_node_normalized_name");

                entity.HasIndex(node => node.Type)
                    .HasDatabaseName("idx_node_type");

                entity.HasIndex(node => node.ParentNodeId)
                    .HasDatabaseName("idx_node_parent");

                entity.HasIndex(node => node.RepositorySnapshotId)
                    .HasDatabaseName("idx_node_snapshot");

                entity.HasIndex(node => new { node.RepositorySnapshotId, node.SerializedName })
                    .IsUnique()
                    .HasDatabaseName("idx_node_unique");

                entity.HasOne(node => node.ParentNode)
                    .WithMany(parent => parent.ChildNodes)
                    .HasForeignKey(node => node.ParentNodeId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(node => node.OutgoingEdges)
                    .WithOne(edge => edge.SourceNode)
                    .HasForeignKey(edge => edge.SourceNodeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(node => node.IncomingEdges)
                    .WithOne(edge => edge.TargetNode)
                    .HasForeignKey(edge => edge.TargetNodeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigureCodeEdge(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CodeEdge>(entity =>
            {
                entity.HasKey(edge => edge.Id);

                entity.HasIndex(edge => edge.SourceNodeId)
                    .HasDatabaseName("idx_edge_source");

                entity.HasIndex(edge => edge.TargetNodeId)
                    .HasDatabaseName("idx_edge_target");

                entity.HasIndex(edge => edge.Type)
                    .HasDatabaseName("idx_edge_type");

                entity.HasIndex(edge => edge.RepositorySnapshotId)
                    .HasDatabaseName("idx_edge_snapshot");

                entity.HasIndex(edge => new { edge.SourceNodeId, edge.TargetNodeId, edge.Type })
                    .HasDatabaseName("idx_edge_source_target_type");
            });
        }

        private static void ConfigureSourceFile(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SourceFile>(entity =>
            {
                entity.HasKey(file => file.Id);

                entity.Property(file => file.Path)
                    .IsRequired()
                    .HasMaxLength(1000);

                entity.Property(file => file.Language)
                    .HasMaxLength(50)
                    .HasDefaultValue("csharp");

                entity.Property(file => file.FileHash)
                    .HasMaxLength(64);

                entity.HasIndex(file => new { file.RepositorySnapshotId, file.Path })
                    .IsUnique()
                    .HasDatabaseName("idx_file_path");

                entity.HasIndex(file => file.FileHash)
                    .HasDatabaseName("idx_file_hash");

                entity.HasIndex(file => file.RepositorySnapshotId)
                    .HasDatabaseName("idx_file_snapshot");

                entity.HasMany(file => file.SourceLocations)
                    .WithOne(location => location.File)
                    .HasForeignKey(location => location.FileId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigureSourceLocation(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SourceLocation>(entity =>
            {
                entity.HasKey(location => location.Id);

                entity.HasIndex(location => location.FileId)
                    .HasDatabaseName("idx_source_location_file");

                entity.HasIndex(location => new { location.FileId, location.StartLine, location.StartColumn })
                    .HasDatabaseName("idx_source_location_position");

                entity.HasMany(location => location.Occurrences)
                    .WithOne(occurrence => occurrence.SourceLocation)
                    .HasForeignKey(occurrence => occurrence.SourceLocationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigureOccurrence(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Occurrence>(entity =>
            {
                entity.HasKey(occurrence => new { occurrence.ElementId, occurrence.SourceLocationId });

                entity.HasIndex(occurrence => occurrence.ElementId)
                    .HasDatabaseName("idx_occurrence_element");

                entity.HasIndex(occurrence => occurrence.SourceLocationId)
                    .HasDatabaseName("idx_occurrence_location");
            });
        }

        private static void ConfigureIndexingError(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IndexingError>(entity =>
            {
                entity.HasKey(error => error.Id);

                entity.Property(error => error.Message)
                    .HasMaxLength(4000);

                entity.HasIndex(error => error.RepositorySnapshotId)
                    .HasDatabaseName("idx_error_snapshot");

                entity.HasIndex(error => error.FileId)
                    .HasDatabaseName("idx_error_file");

                entity.HasOne(error => error.RepositorySnapshot)
                    .WithMany()
                    .HasForeignKey(error => error.RepositorySnapshotId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(error => error.File)
                    .WithMany()
                    .HasForeignKey(error => error.FileId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
