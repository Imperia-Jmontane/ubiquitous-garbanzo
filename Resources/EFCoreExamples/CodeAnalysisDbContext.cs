// ============================================================================
// REFERENCE FILE: EF Core Configuration for Code Analysis
//
// INTEGRATION INSTRUCTIONS:
// This file shows entity configurations to ADD to the existing ApplicationDbContext.
// DO NOT create a separate DbContext - add these to MyApp/MyApp/Data/ApplicationDbContext.cs
//
// STEP 1: Create domain entities in MyApp/MyApp/Domain/CodeAnalysis/
// STEP 2: Add DbSet<> properties to ApplicationDbContext
// STEP 3: Add configuration calls in OnModelCreating
// STEP 4: Run: dotnet ef migrations add AddCodeAnalysis
//
// The existing ApplicationDbContext already has:
// - SQLite DateTimeOffset converters (reuse them!)
// - Proper index naming conventions
// - Cascade delete patterns
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;

namespace MyApp.CodeAnalysis.Reference
{
    // =========================================================================
    // EXAMPLE: How to extend ApplicationDbContext
    //
    // In your actual ApplicationDbContext.cs, add:
    //
    //     public DbSet<IndexedRepository> IndexedRepositories { get; set; } = null!;
    //     public DbSet<CodeNode> CodeNodes { get; set; } = null!;
    //     public DbSet<CodeEdge> CodeEdges { get; set; } = null!;
    //     public DbSet<SourceFile> SourceFiles { get; set; } = null!;
    //     public DbSet<SourceLocation> SourceLocations { get; set; } = null!;
    //     public DbSet<Occurrence> Occurrences { get; set; } = null!;
    //     public DbSet<IndexingError> IndexingErrors { get; set; } = null!;
    //
    // And in OnModelCreating, call:
    //
    //     ConfigureCodeAnalysisEntities(modelBuilder);
    //
    // The DateTimeOffset conversion is ALREADY CONFIGURED in ApplicationDbContext!
    // =========================================================================

    /// <summary>
    /// Reference implementation showing entity configurations.
    /// Copy the ConfigureCodeAnalysisEntities method to your ApplicationDbContext.
    /// </summary>
    public static class CodeAnalysisEntityConfigurations
    {
        // =====================================================================
        // MAIN CONFIGURATION METHOD
        // Copy this method to ApplicationDbContext and call it from OnModelCreating
        // =====================================================================

        /// <summary>
        /// Call this method from ApplicationDbContext.OnModelCreating
        /// </summary>
        public static void ConfigureCodeAnalysisEntities(ModelBuilder modelBuilder)
        {
            // Apply all Code Analysis entity configurations
            ConfigureIndexedRepository(modelBuilder);
            ConfigureCodeNode(modelBuilder);
            ConfigureCodeEdge(modelBuilder);
            ConfigureSourceFile(modelBuilder);
            ConfigureSourceLocation(modelBuilder);
            ConfigureOccurrence(modelBuilder);
            ConfigureIndexingError(modelBuilder);

            // NOTE: DateTimeOffset conversion is ALREADY handled by ApplicationDbContext
            // Do NOT add it again here
        }

        // =====================================================================
        // INDEXED REPOSITORY CONFIGURATION
        // =====================================================================
        private static void ConfigureIndexedRepository(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IndexedRepository>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.RepositoryId)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.RepositoryPath)
                    .IsRequired()
                    .HasMaxLength(1000);

                entity.Property(e => e.CommitSha)
                    .HasMaxLength(40);  // Git SHA-1 is 40 hex characters

                entity.Property(e => e.BranchName)
                    .HasMaxLength(250);

                entity.Property(e => e.ErrorMessage)
                    .HasMaxLength(4000);

                // IMPORTANT: Indices for fast lookups
                entity.HasIndex(e => e.RepositoryId)
                    .HasDatabaseName("idx_indexed_repository_id");

                entity.HasIndex(e => new { e.RepositoryId, e.CommitSha })
                    .HasDatabaseName("idx_indexed_repository_commit");

                entity.HasIndex(e => e.Status)
                    .HasDatabaseName("idx_indexed_repository_status");

                // Relationship: One repository has many files, nodes, edges
                entity.HasMany(e => e.Files)
                    .WithOne(f => f.RepositorySnapshot)
                    .HasForeignKey(f => f.RepositorySnapshotId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Nodes)
                    .WithOne(n => n.RepositorySnapshot)
                    .HasForeignKey(n => n.RepositorySnapshotId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Edges)
                    .WithOne(e => e.RepositorySnapshot)
                    .HasForeignKey(e => e.RepositorySnapshotId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        // =====================================================================
        // CODE NODE CONFIGURATION
        // =====================================================================
        private static void ConfigureCodeNode(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CodeNode>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.SerializedName)
                    .IsRequired()
                    .HasMaxLength(2000);  // Fully qualified names can be long

                entity.Property(e => e.DisplayName)
                    .HasMaxLength(500);

                entity.Property(e => e.NormalizedName)
                    .HasMaxLength(500);

                // CRITICAL INDICES for performance
                entity.HasIndex(e => e.SerializedName)
                    .HasDatabaseName("idx_node_serialized_name");

                entity.HasIndex(e => e.DisplayName)
                    .HasDatabaseName("idx_node_display_name");

                // For case-insensitive search using LIKE
                entity.HasIndex(e => e.NormalizedName)
                    .HasDatabaseName("idx_node_normalized_name");

                entity.HasIndex(e => e.Type)
                    .HasDatabaseName("idx_node_type");

                entity.HasIndex(e => e.ParentNodeId)
                    .HasDatabaseName("idx_node_parent");

                entity.HasIndex(e => e.RepositorySnapshotId)
                    .HasDatabaseName("idx_node_snapshot");

                // UNIQUE constraint: Same symbol cannot exist twice in same snapshot
                entity.HasIndex(e => new { e.RepositorySnapshotId, e.SerializedName })
                    .IsUnique()
                    .HasDatabaseName("idx_node_unique");

                // Self-referencing relationship for containment hierarchy
                entity.HasOne(e => e.ParentNode)
                    .WithMany(e => e.ChildNodes)
                    .HasForeignKey(e => e.ParentNodeId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Outgoing edges (this node is the source)
                entity.HasMany(e => e.OutgoingEdges)
                    .WithOne(e => e.SourceNode)
                    .HasForeignKey(e => e.SourceNodeId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Incoming edges (this node is the target)
                entity.HasMany(e => e.IncomingEdges)
                    .WithOne(e => e.TargetNode)
                    .HasForeignKey(e => e.TargetNodeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        // =====================================================================
        // CODE EDGE CONFIGURATION
        // =====================================================================
        private static void ConfigureCodeEdge(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CodeEdge>(entity =>
            {
                entity.HasKey(e => e.Id);

                // CRITICAL INDICES for graph traversal
                entity.HasIndex(e => e.SourceNodeId)
                    .HasDatabaseName("idx_edge_source");

                entity.HasIndex(e => e.TargetNodeId)
                    .HasDatabaseName("idx_edge_target");

                entity.HasIndex(e => e.Type)
                    .HasDatabaseName("idx_edge_type");

                entity.HasIndex(e => e.RepositorySnapshotId)
                    .HasDatabaseName("idx_edge_snapshot");

                // Composite index for finding specific relationships
                entity.HasIndex(e => new { e.SourceNodeId, e.TargetNodeId, e.Type })
                    .HasDatabaseName("idx_edge_source_target_type");
            });
        }

        // =====================================================================
        // SOURCE FILE CONFIGURATION
        // =====================================================================
        private static void ConfigureSourceFile(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SourceFile>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Path)
                    .IsRequired()
                    .HasMaxLength(1000);

                entity.Property(e => e.Language)
                    .HasMaxLength(50)
                    .HasDefaultValue("csharp");

                entity.Property(e => e.FileHash)
                    .HasMaxLength(64);  // SHA-256 is 64 hex characters

                // UNIQUE: Same file path cannot exist twice in same snapshot
                entity.HasIndex(e => new { e.RepositorySnapshotId, e.Path })
                    .IsUnique()
                    .HasDatabaseName("idx_file_path");

                // For incremental indexing (finding changed files)
                entity.HasIndex(e => e.FileHash)
                    .HasDatabaseName("idx_file_hash");

                entity.HasIndex(e => e.RepositorySnapshotId)
                    .HasDatabaseName("idx_file_snapshot");

                entity.HasMany(e => e.SourceLocations)
                    .WithOne(l => l.File)
                    .HasForeignKey(l => l.FileId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        // =====================================================================
        // SOURCE LOCATION CONFIGURATION
        // =====================================================================
        private static void ConfigureSourceLocation(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SourceLocation>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.FileId)
                    .HasDatabaseName("idx_source_location_file");

                // For finding all locations in a specific area of a file
                entity.HasIndex(e => new { e.FileId, e.StartLine, e.StartColumn })
                    .HasDatabaseName("idx_source_location_position");

                entity.HasMany(e => e.Occurrences)
                    .WithOne(o => o.SourceLocation)
                    .HasForeignKey(o => o.SourceLocationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        // =====================================================================
        // OCCURRENCE CONFIGURATION (junction table)
        // =====================================================================
        private static void ConfigureOccurrence(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Occurrence>(entity =>
            {
                // Composite primary key
                entity.HasKey(e => new { e.ElementId, e.SourceLocationId });

                entity.HasIndex(e => e.ElementId)
                    .HasDatabaseName("idx_occurrence_element");

                entity.HasIndex(e => e.SourceLocationId)
                    .HasDatabaseName("idx_occurrence_location");
            });
        }

        // =====================================================================
        // INDEXING ERROR CONFIGURATION
        // =====================================================================
        private static void ConfigureIndexingError(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IndexingError>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Message)
                    .HasMaxLength(4000);

                entity.HasIndex(e => e.RepositorySnapshotId)
                    .HasDatabaseName("idx_error_snapshot");

                entity.HasIndex(e => e.FileId)
                    .HasDatabaseName("idx_error_file");

                entity.HasOne(e => e.RepositorySnapshot)
                    .WithMany()
                    .HasForeignKey(e => e.RepositorySnapshotId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.File)
                    .WithMany()
                    .HasForeignKey(e => e.FileId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }

        // =====================================================================
        // NOTE: DATETIME OFFSET CONVERSION
        // The existing ApplicationDbContext already handles DateTimeOffset conversion
        // for SQLite. See ApplicationDbContext.cs lines 28-56 for the implementation.
        // DO NOT duplicate this configuration - it will cause conflicts.
        // =====================================================================
    }

    // =========================================================================
    // ENTITY CLASSES (simplified - actual implementation would be in Domain)
    // =========================================================================

    public class IndexedRepository
    {
        public long Id { get; set; }
        public string RepositoryId { get; set; } = string.Empty;
        public string RepositoryPath { get; set; } = string.Empty;
        public string? CommitSha { get; set; }
        public string? BranchName { get; set; }
        public DateTime IndexedAtUtc { get; set; }
        public IndexingStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public int FilesIndexed { get; set; }
        public int SymbolsCollected { get; set; }
        public int ReferencesCollected { get; set; }
        public long IndexingDurationMs { get; set; }

        public ICollection<SourceFile> Files { get; set; } = new List<SourceFile>();
        public ICollection<CodeNode> Nodes { get; set; } = new List<CodeNode>();
        public ICollection<CodeEdge> Edges { get; set; } = new List<CodeEdge>();
    }

    public class CodeNode
    {
        public long Id { get; set; }
        public long RepositorySnapshotId { get; set; }
        public CSharpSymbolKind Type { get; set; }
        public string SerializedName { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? NormalizedName { get; set; }
        public long? ParentNodeId { get; set; }
        public int? Accessibility { get; set; }
        public bool IsStatic { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsOverride { get; set; }
        public bool IsExtensionMethod { get; set; }
        public bool IsAsync { get; set; }
        public bool IsExternal { get; set; }

        public IndexedRepository? RepositorySnapshot { get; set; }
        public CodeNode? ParentNode { get; set; }
        public ICollection<CodeNode> ChildNodes { get; set; } = new List<CodeNode>();
        public ICollection<CodeEdge> OutgoingEdges { get; set; } = new List<CodeEdge>();
        public ICollection<CodeEdge> IncomingEdges { get; set; } = new List<CodeEdge>();
        public ICollection<Occurrence> Occurrences { get; set; } = new List<Occurrence>();
    }

    public class CodeEdge
    {
        public long Id { get; set; }
        public long RepositorySnapshotId { get; set; }
        public CSharpReferenceKind Type { get; set; }
        public long SourceNodeId { get; set; }
        public long TargetNodeId { get; set; }

        public IndexedRepository? RepositorySnapshot { get; set; }
        public CodeNode SourceNode { get; set; } = null!;
        public CodeNode TargetNode { get; set; } = null!;
        public ICollection<Occurrence> Occurrences { get; set; } = new List<Occurrence>();
    }

    public class SourceFile
    {
        public long Id { get; set; }
        public long RepositorySnapshotId { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Language { get; set; } = "csharp";
        public string? FileHash { get; set; }
        public DateTime? ModificationTime { get; set; }
        public bool IsIndexed { get; set; }
        public bool IsComplete { get; set; } = true;
        public int? LineCount { get; set; }

        public IndexedRepository RepositorySnapshot { get; set; } = null!;
        public ICollection<SourceLocation> SourceLocations { get; set; } = new List<SourceLocation>();
    }

    public class SourceLocation
    {
        public long Id { get; set; }
        public long FileId { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public LocationType Type { get; set; }

        public SourceFile File { get; set; } = null!;
        public ICollection<Occurrence> Occurrences { get; set; } = new List<Occurrence>();
    }

    public class Occurrence
    {
        public long ElementId { get; set; }
        public long SourceLocationId { get; set; }

        public SourceLocation SourceLocation { get; set; } = null!;
    }

    public class IndexingError
    {
        public long Id { get; set; }
        public long RepositorySnapshotId { get; set; }
        public string? Message { get; set; }
        public bool IsFatal { get; set; }
        public long? FileId { get; set; }
        public int? Line { get; set; }
        public int? Column { get; set; }

        public IndexedRepository RepositorySnapshot { get; set; } = null!;
        public SourceFile? File { get; set; }
    }

    // Enums (duplicated here for completeness)
    public enum IndexingStatus { Queued = 0, Running = 1, Completed = 2, Failed = 3, Cancelled = 4 }
    public enum CSharpSymbolKind { Unknown = 0, Namespace = 1, Class = 10, Struct = 11, Interface = 12, Enum = 13, Method = 22 }
    public enum CSharpReferenceKind { Unknown = 0, Inheritance = 1, InterfaceImplementation = 2, Call = 10, Contains = 30 }
    public enum LocationType { Definition = 0, Reference = 1, Scope = 2 }
}
