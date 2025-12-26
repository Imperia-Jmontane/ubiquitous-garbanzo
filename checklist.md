# Implementation Checklist: Roslyn Code Analysis & Graph Visualization

> **Purpose:** Integrate Sourcetrail-like code analysis functionality using Roslyn for C# projects with interactive graph visualization in the webapp.
>
> **Estimated Scope:** ~6 phases, each phase should be completable independently

---

## Prerequisites

Before starting, ensure you have:

- [ ] .NET 9 SDK installed (`dotnet --version` should show 9.x)
- [ ] Visual Studio 2022 or VS Code with C# extension
- [ ] The Flow repository cloned and building successfully
- [ ] Read the `Resources/README.md` file for an overview of reference materials
- [ ] Reviewed the `code_analysis_integration_plan.md` for detailed context

---

## Architecture Decision (Read First!)

### Project Structure

This feature uses a **separate `MyApp.CodeAnalysis` project** with proper internal layering. This decision was made because:

1. CodeAnalysis is a major, self-contained feature with its own database
2. It has complex infrastructure (Roslyn, MSBuild, graph queries)
3. Separation allows isolated testing and potential reuse
4. Keeps the main MyApp project focused on web UI/routing

**Internal structure of MyApp.CodeAnalysis:**
```
MyApp.CodeAnalysis/
├── Domain/
│   ├── Enums/          (CSharpSymbolKind, CSharpReferenceKind, etc.)
│   ├── Entities/       (CodeNode, CodeEdge, SourceFile, etc.)
│   └── Services/       (ICodeGraphRepository, ICodeIndexer, IIndexingJobService)
├── Application/
│   └── DTOs/           (GraphData, GraphNode, IndexingResult, IndexingStatus, etc.)
├── Infrastructure/
│   ├── Persistence/    (CodeAnalysisDbContext, CodeGraphRepository)
│   ├── Roslyn/         (WorkspaceLoader, SymbolCollector, ReferenceCollector)
│   ├── Jobs/           (IndexingJobService - background processing)
│   └── Migrations/     (EF Core migrations - separate folder)
└── MyApp.CodeAnalysis.csproj
```

### Database Strategy

- **Separate SQLite database** (`code_analysis.db`) from the main application database
- Configuration via `appsettings.json` under `CodeAnalysis:ConnectionString`
- Migrations in separate folder to avoid conflicts

---

## Phase 1: Project Setup and Domain Layer

### 1.1 Create New Project

- [ ] Create a new class library project named `MyApp.CodeAnalysis`
  ```bash
  cd /path/to/Flow/MyApp
  dotnet new classlib -n MyApp.CodeAnalysis -f net9.0
  dotnet sln add MyApp.CodeAnalysis/MyApp.CodeAnalysis.csproj
  ```

- [ ] Add project reference from `MyApp` to `MyApp.CodeAnalysis`
  - Open `MyApp/MyApp.csproj`
  - Add inside `<ItemGroup>`:
    ```xml
    <ProjectReference Include="..\MyApp.CodeAnalysis\MyApp.CodeAnalysis.csproj" />
    ```

- [ ] Add required NuGet packages to `MyApp.CodeAnalysis.csproj`:
  ```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.8.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.8.0" />
    <PackageReference Include="Microsoft.Build.Locator" Version="1.6.10" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
  </ItemGroup>
  ```

- [ ] Create the folder structure:
  ```bash
  mkdir -p MyApp.CodeAnalysis/Domain/Enums
  mkdir -p MyApp.CodeAnalysis/Domain/Entities
  mkdir -p MyApp.CodeAnalysis/Domain/Services
  mkdir -p MyApp.CodeAnalysis/Application/DTOs
  mkdir -p MyApp.CodeAnalysis/Infrastructure/Persistence
  mkdir -p MyApp.CodeAnalysis/Infrastructure/Roslyn
  mkdir -p MyApp.CodeAnalysis/Infrastructure/Jobs
  ```

- [ ] Verify the project builds without errors:
  ```bash
  dotnet build MyApp.CodeAnalysis/MyApp.CodeAnalysis.csproj
  ```

### 1.2 Create Domain Enums

Create folder structure: `MyApp.CodeAnalysis/Domain/Enums/`

- [ ] Create `CSharpSymbolKind.cs`:
  - Location: `MyApp.CodeAnalysis/Domain/Enums/CSharpSymbolKind.cs`
  - Copy enum values from `Resources/SourcetrailReference/DatabaseSchema.sql` (lines with CSharpSymbolKind comments)
  - Include: Unknown, Namespace, Assembly, Module, Class, Struct, Interface, Enum, Delegate, Record, RecordStruct, Field, Property, Method, Constructor, Destructor, Operator, Indexer, Event, EnumMember, LocalVariable, Parameter, TypeParameter, File, Using, Attribute

- [ ] Create `CSharpReferenceKind.cs`:
  - Location: `MyApp.CodeAnalysis/Domain/Enums/CSharpReferenceKind.cs`
  - Copy enum values from `Resources/SourcetrailReference/DatabaseSchema.sql` (lines with CSharpReferenceKind comments)
  - Include: Unknown, Inheritance, InterfaceImplementation, Call, TypeUsage, Override, FieldAccess, PropertyAccess, EventAccess, Contains, Import, TypeArgument, AttributeUsage, Instantiation, Cast, Throw, Catch

- [ ] Create `LocationType.cs`:
  - Location: `MyApp.CodeAnalysis/Domain/Enums/LocationType.cs`
  - Values: Definition = 0, Reference = 1, Scope = 2

- [ ] Create `DefinitionKind.cs`:
  - Location: `MyApp.CodeAnalysis/Domain/Enums/DefinitionKind.cs`
  - Values: Explicit = 0, Implicit = 1

- [ ] Create `IndexingStatus.cs`:
  - Location: `MyApp.CodeAnalysis/Domain/Enums/IndexingStatus.cs`
  - Values: Queued = 0, Running = 1, Completed = 2, Failed = 3, Cancelled = 4
  - **Note:** This is for background job tracking

### 1.3 Create Domain Entities

Create folder structure: `MyApp.CodeAnalysis/Domain/Entities/`

- [ ] Create `CodeElement.cs`:
  - Properties: `long Id` (auto-increment primary key)
  - **Note:** Using `long` to avoid overflow on large repositories

- [ ] Create `CodeNode.cs`:
  - Inherits from or references `CodeElement`
  - Properties:
    - `long Id` (PK, same as element ID)
    - `CSharpSymbolKind Type`
    - `string SerializedName` (fully qualified name, NOT NULL)
    - `string? DisplayName` (human-readable name)
    - `string? NormalizedName` (lowercase for search, indexed)
    - `int? Accessibility` (0=Public, 1=Protected, etc.)
    - `bool IsStatic`
    - `bool IsAbstract`
    - `bool IsVirtual`
    - `bool IsOverride`
    - `bool IsExtensionMethod`
    - `bool IsAsync`
    - `long? ParentNodeId` (FK to parent namespace/type for containment hierarchy)
    - `long? RepositorySnapshotId` (FK to IndexedRepository, for multi-repo support)
  - Navigation properties:
    - `ICollection<CodeEdge> OutgoingEdges`
    - `ICollection<CodeEdge> IncomingEdges`
    - `ICollection<Occurrence> Occurrences`
    - `CodeNode? ParentNode`
    - `ICollection<CodeNode> ChildNodes`
    - `IndexedRepository? RepositorySnapshot`

- [ ] Create `CodeEdge.cs`:
  - Properties:
    - `long Id` (PK)
    - `CSharpReferenceKind Type`
    - `long SourceNodeId` (FK to CodeNode)
    - `long TargetNodeId` (FK to CodeNode)
    - `long? RepositorySnapshotId` (FK to IndexedRepository)
  - Navigation properties:
    - `CodeNode SourceNode`
    - `CodeNode TargetNode`
    - `ICollection<Occurrence> Occurrences`
    - `IndexedRepository? RepositorySnapshot`

- [ ] Create `IndexedRepository.cs` (NEW - for multi-repo and versioning support):
  - Properties:
    - `long Id` (PK)
    - `string RepositoryId` (matches LocalRepository identifier from existing Domain)
    - `string RepositoryPath` (absolute path)
    - `string? CommitSha` (current indexed commit)
    - `string? BranchName` (current indexed branch)
    - `DateTime IndexedAtUtc`
    - `IndexingStatus Status`
    - `string? ErrorMessage` (if Status == Failed)
    - `int FilesIndexed`
    - `int SymbolsCollected`
    - `int ReferencesCollected`
    - `TimeSpan IndexingDuration`
  - Navigation properties:
    - `ICollection<SourceFile> Files`
    - `ICollection<CodeNode> Nodes`
    - `ICollection<CodeEdge> Edges`

- [ ] Create `SourceFile.cs`:
  - Properties:
    - `long Id` (PK, also references CodeNode.Id)
    - `string Path` (relative path within repository, UNIQUE per repository)
    - `string Language` (default: "csharp")
    - `string? FileHash` (SHA256 for incremental indexing)
    - `DateTime? ModificationTime`
    - `bool IsIndexed`
    - `bool IsComplete`
    - `int? LineCount`
    - `long RepositorySnapshotId` (FK to IndexedRepository)
  - Navigation properties:
    - `ICollection<SourceLocation> SourceLocations`
    - `IndexedRepository RepositorySnapshot`

- [ ] Create `SourceLocation.cs`:
  - Properties:
    - `long Id` (PK, auto-increment)
    - `long FileId` (FK to SourceFile)
    - `int StartLine` (1-based)
    - `int StartColumn` (1-based)
    - `int EndLine` (1-based)
    - `int EndColumn` (1-based)
    - `int StartOffset` (0-based byte offset for precise navigation)
    - `int EndOffset` (0-based byte offset)
    - `LocationType Type`
  - Navigation properties:
    - `SourceFile File`
    - `ICollection<Occurrence> Occurrences`

- [ ] Create `Occurrence.cs`:
  - This is a junction table (many-to-many)
  - Properties:
    - `long ElementId` (FK, part of composite PK)
    - `long SourceLocationId` (FK, part of composite PK)
  - Navigation properties:
    - `SourceLocation SourceLocation`

- [ ] Create `IndexingError.cs`:
  - Properties:
    - `long Id` (PK)
    - `long RepositorySnapshotId` (FK to IndexedRepository)
    - `string? Message`
    - `bool IsFatal`
    - `long? FileId` (FK to SourceFile)
    - `int? Line`
    - `int? Column`
  - Navigation properties:
    - `IndexedRepository RepositorySnapshot`
    - `SourceFile? File`

### 1.4 Create Domain Interfaces

Create folder structure: `MyApp.CodeAnalysis/Domain/Services/`

- [ ] Create `ICodeGraphRepository.cs`:
  ```csharp
  public interface ICodeGraphRepository
  {
      // Transaction management
      Task BeginTransactionAsync(CancellationToken ct);
      Task CommitTransactionAsync(CancellationToken ct);
      Task RollbackTransactionAsync(CancellationToken ct);

      // Repository snapshot operations
      Task<long> CreateRepositorySnapshotAsync(string repositoryId, string repositoryPath,
          string? commitSha, string? branchName, CancellationToken ct);
      Task<IndexedRepository?> GetRepositorySnapshotAsync(string repositoryId, CancellationToken ct);
      Task UpdateRepositoryStatusAsync(long snapshotId, IndexingStatus status,
          string? errorMessage, int filesIndexed, int symbolsCollected,
          int referencesCollected, TimeSpan duration, CancellationToken ct);

      // File operations
      Task<long> RecordFileAsync(long snapshotId, string relativePath, string language,
          string? fileHash, CancellationToken ct);
      Task<long?> GetFileIdAsync(long snapshotId, string relativePath, CancellationToken ct);
      Task<bool> IsFileChangedAsync(long snapshotId, string relativePath, string fileHash,
          CancellationToken ct);

      // Node operations
      long RecordNode(long snapshotId, string serializedName, string? displayName,
          CSharpSymbolKind kind, long? parentNodeId, int? accessibility,
          bool isStatic, bool isAbstract, bool isVirtual, bool isOverride,
          bool isExtensionMethod, bool isAsync);
      long GetOrCreateNodeId(long snapshotId, string serializedName);
      long? TryGetNodeId(long snapshotId, string serializedName);

      // External symbol operations (for symbols from referenced assemblies)
      long RecordExternalNode(long snapshotId, string serializedName, string? displayName,
          CSharpSymbolKind kind);

      // Edge operations
      long RecordEdge(long snapshotId, long sourceNodeId, long targetNodeId,
          CSharpReferenceKind kind);

      // Location operations
      void RecordSourceLocation(long nodeId, long fileId, int startLine, int startColumn,
          int endLine, int endColumn, int startOffset, int endOffset, LocationType locationType);
      void RecordOccurrence(long elementId, long fileId, int startLine, int startColumn,
          int endLine, int endColumn, int startOffset, int endOffset);

      // Query operations
      Task<GraphData> GetGraphDataAsync(GraphQueryOptions options, CancellationToken ct);
      Task<List<ReferenceLocation>> GetSymbolReferencesAsync(long symbolId, CancellationToken ct);
      Task<List<SymbolSearchResult>> SearchSymbolsAsync(string repositoryId, string query,
          int limit, CancellationToken ct);
  }
  ```

- [ ] Create `ICodeIndexer.cs`:
  ```csharp
  public interface ICodeIndexer
  {
      Task<IndexingResult> IndexSolutionAsync(long snapshotId, string solutionPath,
          CancellationToken ct);
      Task<IndexingResult> IndexProjectAsync(long snapshotId, string projectPath,
          CancellationToken ct);
      Task<IndexingResult> IndexFileAsync(long snapshotId, string filePath,
          CancellationToken ct);
  }
  ```

- [ ] Create `IIndexingJobService.cs` (NEW - for background job processing):
  ```csharp
  public interface IIndexingJobService
  {
      /// <summary>
      /// Queues a repository for indexing. Returns immediately.
      /// </summary>
      Task<long> QueueIndexingAsync(string repositoryId, CancellationToken ct);

      /// <summary>
      /// Gets the current status of an indexing job.
      /// </summary>
      Task<IndexingJobStatus?> GetJobStatusAsync(string repositoryId, CancellationToken ct);

      /// <summary>
      /// Cancels a running or queued indexing job.
      /// </summary>
      Task<bool> CancelJobAsync(string repositoryId, CancellationToken ct);
  }
  ```

### 1.5 Create DTOs/Models

Create folder structure: `MyApp.CodeAnalysis/Application/DTOs/`

- [ ] Create `IndexingResult.cs`:
  - Properties: `long SnapshotId`, `int FilesIndexed`, `int SymbolsCollected`, `int ReferencesCollected`, `TimeSpan Duration`, `List<string> Errors`, `bool PartialSuccess` (true if indexed despite some errors)

- [ ] Create `IndexingJobStatus.cs` (NEW):
  - Properties: `string RepositoryId`, `IndexingStatus Status`, `DateTime? StartedAtUtc`, `DateTime? CompletedAtUtc`, `int? FilesIndexed`, `int? TotalFiles`, `string? CurrentFile`, `string? ErrorMessage`, `int ProgressPercent`

- [ ] Create `GraphQueryOptions.cs`:
  - Properties:
    - `string RepositoryId` (required - identify which repository)
    - `int MaxDepth` (default 2)
    - `int MaxNodes` (default 100, for pagination)
    - `int MaxEdges` (default 500, for pagination)
    - `bool IncludeMembers` (default false)
    - `long? RootNodeId` (optional - focus on specific node)
    - `string? NamespaceFilter` (optional - filter by namespace prefix)
    - `List<CSharpSymbolKind>? SymbolKindFilter` (optional)

- [ ] Create `GraphData.cs`:
  - Properties: `List<GraphNode> Nodes`, `List<GraphEdge> Edges`, `bool HasMore` (for pagination)

- [ ] Create `GraphNode.cs`:
  - Properties:
    - `long Id`
    - `string SerializedName`
    - `string? DisplayName`
    - `string Type` (string enum name, e.g., "Class", "Method" - better for frontend)
    - `string? FilePath`
    - `int? Line`
    - `int? Column`
    - `long? ParentId` (for containment hierarchy in UI)

- [ ] Create `GraphEdge.cs`:
  - Properties:
    - `long Id`
    - `long SourceNodeId`
    - `long TargetNodeId`
    - `string Type` (string enum name, e.g., "Call", "Inheritance")

- [ ] Create `ReferenceLocation.cs`:
  - Properties: `string FilePath`, `int Line`, `int Column`, `int EndLine`, `int EndColumn`, `string? Context` (surrounding code snippet)

- [ ] Create `SymbolSearchResult.cs`:
  - Properties: `long Id`, `string DisplayName`, `string SerializedName`, `string Kind` (string enum name), `string? FilePath`, `int? Line`

### 1.6 Verification

- [ ] Build the project and fix any errors:
  ```bash
  dotnet build MyApp.CodeAnalysis/MyApp.CodeAnalysis.csproj
  ```
- [ ] Verify all files are in the correct locations
- [ ] Ensure no `var` keywords are used (use explicit types)
- [ ] Ensure PascalCase for all public members

---

## Phase 2: Infrastructure Layer - Database

### 2.1 Add Configuration to appsettings.json

- [ ] Add CodeAnalysis section to `MyApp/appsettings.json`:
  ```json
  {
    "CodeAnalysis": {
      "ConnectionString": "Data Source={AppDataPath}/code_analysis.db",
      "MaxIndexingConcurrency": 1,
      "IndexingTimeoutMinutes": 30
    }
  }
  ```

- [ ] Create `CodeAnalysisOptions.cs` in `MyApp.CodeAnalysis/Application/`:
  ```csharp
  public sealed class CodeAnalysisOptions
  {
      public string ConnectionString { get; set; } = "Data Source=code_analysis.db";
      public int MaxIndexingConcurrency { get; set; } = 1;
      public int IndexingTimeoutMinutes { get; set; } = 30;
  }
  ```

### 2.2 Create DbContext

Create folder structure: `MyApp.CodeAnalysis/Infrastructure/Persistence/`

- [ ] Create `CodeAnalysisDbContext.cs`:
  - Inherit from `DbContext`
  - Add `DbSet<T>` properties for each entity:
    - `DbSet<IndexedRepository> IndexedRepositories`
    - `DbSet<CodeNode> Nodes`
    - `DbSet<CodeEdge> Edges`
    - `DbSet<SourceFile> Files`
    - `DbSet<SourceLocation> SourceLocations`
    - `DbSet<Occurrence> Occurrences`
    - `DbSet<IndexingError> Errors`
  - Override `OnModelCreating` to configure:
    - Primary keys
    - Foreign key relationships
    - Indices (VERY IMPORTANT for performance)
    - Unique constraints

- [ ] Configure entity relationships in `OnModelCreating`:
  ```csharp
  // Key indices for performance (from Resources/SourcetrailReference/DatabaseSchema.sql)

  // Node lookups by name (for search)
  modelBuilder.Entity<CodeNode>()
      .HasIndex(n => n.SerializedName);
  modelBuilder.Entity<CodeNode>()
      .HasIndex(n => n.DisplayName);
  modelBuilder.Entity<CodeNode>()
      .HasIndex(n => n.NormalizedName);  // For LIKE searches
  modelBuilder.Entity<CodeNode>()
      .HasIndex(n => n.Type);
  modelBuilder.Entity<CodeNode>()
      .HasIndex(n => new { n.RepositorySnapshotId, n.SerializedName })
      .IsUnique();

  // Edge lookups (for graph traversal)
  modelBuilder.Entity<CodeEdge>()
      .HasIndex(e => e.SourceNodeId);
  modelBuilder.Entity<CodeEdge>()
      .HasIndex(e => e.TargetNodeId);
  modelBuilder.Entity<CodeEdge>()
      .HasIndex(e => e.Type);
  modelBuilder.Entity<CodeEdge>()
      .HasIndex(e => new { e.SourceNodeId, e.TargetNodeId, e.Type });

  // Source location lookups (for navigation)
  modelBuilder.Entity<SourceLocation>()
      .HasIndex(l => l.FileId);
  modelBuilder.Entity<SourceLocation>()
      .HasIndex(l => new { l.FileId, l.StartLine, l.StartColumn });

  // Occurrence lookups (for finding all usages)
  modelBuilder.Entity<Occurrence>()
      .HasKey(o => new { o.ElementId, o.SourceLocationId });
  modelBuilder.Entity<Occurrence>()
      .HasIndex(o => o.ElementId);
  modelBuilder.Entity<Occurrence>()
      .HasIndex(o => o.SourceLocationId);

  // File lookups
  modelBuilder.Entity<SourceFile>()
      .HasIndex(f => new { f.RepositorySnapshotId, f.Path })
      .IsUnique();
  modelBuilder.Entity<SourceFile>()
      .HasIndex(f => f.FileHash);

  // Repository snapshot lookups
  modelBuilder.Entity<IndexedRepository>()
      .HasIndex(r => r.RepositoryId);
  modelBuilder.Entity<IndexedRepository>()
      .HasIndex(r => new { r.RepositoryId, r.CommitSha });
  ```

- [ ] Configure relationships:
  - `CodeNode` -> `CodeEdge` (one-to-many for OutgoingEdges via SourceNodeId)
  - `CodeNode` -> `CodeEdge` (one-to-many for IncomingEdges via TargetNodeId)
  - `CodeNode` -> `CodeNode` (self-referencing for ParentNode/ChildNodes)
  - `SourceFile` -> `SourceLocation` (one-to-many)
  - `IndexedRepository` -> `SourceFile` (one-to-many)
  - `IndexedRepository` -> `CodeNode` (one-to-many)
  - `Occurrence` composite key (ElementId, SourceLocationId)

### 2.3 Create Repository Implementation

- [ ] Create `CodeGraphRepository.cs`:
  - Implement `ICodeGraphRepository`
  - Inject `CodeAnalysisDbContext` via constructor
  - Use a `Dictionary<string, long>` cache for node name -> ID mapping (performance optimization)
  - Use a `Dictionary<string, long>` cache for file path -> ID mapping
  - Reference: `Resources/RoslynExamples/WorkspaceLoader.cs` for the caching pattern

- [ ] Implement transaction methods:
  - `BeginTransactionAsync`: Start EF Core transaction
  - `CommitTransactionAsync`: Commit transaction and clear caches
  - `RollbackTransactionAsync`: Rollback and clear caches

- [ ] Implement `RecordNode`:
  - Check cache first
  - If not in cache, check database
  - If not in database, create new node
  - **Set NormalizedName = DisplayName?.ToLowerInvariant()** for search
  - Add to cache and return ID

- [ ] Implement `RecordExternalNode`:
  - For symbols from referenced assemblies (System.String, etc.)
  - Store minimal info (just serialized name, display name, kind)
  - Mark with a flag or store separately to distinguish from source-defined symbols

- [ ] Implement `RecordEdge`:
  - Create edge entity
  - Save to database
  - Return edge ID

- [ ] Implement `IsFileChangedAsync`:
  - Check if file hash differs from stored hash
  - Used for incremental indexing

- [ ] Implement `GetGraphDataAsync`:
  - Build query based on options (depth, rootNodeId, includeMembers, maxNodes, maxEdges)
  - **Respect maxNodes and maxEdges limits** (pagination)
  - Use recursive CTE or multiple queries to get related nodes
  - Set `HasMore = true` if more nodes/edges exist beyond limit
  - Map to DTOs with **string enum names** for Type fields

- [ ] Implement `SearchSymbolsAsync`:
  - Use NormalizedName with LIKE query for partial matching
  - Order by relevance (exact match first, then prefix match, then contains)
  - Respect limit parameter

### 2.4 Create Migrations

- [ ] Add EF Core Design package to main project (if not already present):
  ```bash
  dotnet add MyApp/MyApp.csproj package Microsoft.EntityFrameworkCore.Design --version 9.0.0
  ```

- [ ] Create initial migration:
  ```bash
  cd MyApp
  dotnet ef migrations add InitialCodeAnalysis --context CodeAnalysisDbContext --output-dir ../MyApp.CodeAnalysis/Infrastructure/Migrations
  ```

- [ ] Review generated migration and verify it matches the schema
  - Check all indices are created
  - Check foreign keys are correct
  - Check nullable columns are correct

- [ ] Apply migration:
  ```bash
  dotnet ef database update --context CodeAnalysisDbContext
  ```

### 2.5 Register Services in DI

- [ ] In `Program.cs`, add configuration binding:
  ```csharp
  // Bind CodeAnalysis options
  builder.Services.Configure<CodeAnalysisOptions>(
      builder.Configuration.GetSection("CodeAnalysis"));
  ```

- [ ] In `Program.cs`, add DbContext:
  ```csharp
  // Add CodeAnalysis DbContext
  builder.Services.AddDbContext<CodeAnalysisDbContext>((serviceProvider, options) =>
  {
      IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
      string connectionString = configuration.GetValue<string>("CodeAnalysis:ConnectionString")
          ?? "Data Source=code_analysis.db";

      // Replace {AppDataPath} placeholder (matching existing pattern)
      connectionString = connectionString.Replace("{AppDataPath}", appDataPath);

      options.UseSqlite(connectionString);
  });
  ```

- [ ] Register repository:
  ```csharp
  builder.Services.AddScoped<ICodeGraphRepository, CodeGraphRepository>();
  ```

### 2.6 Verification

- [ ] Run the application and verify the database is created at the configured path
- [ ] Verify tables exist with correct schema using SQLite browser or EF Core tools
- [ ] Write a simple unit test that:
  - Creates a CodeAnalysisDbContext
  - Creates a repository snapshot
  - Adds a node
  - Retrieves it
  - Verifies data integrity

---

## Phase 3: Roslyn Indexer Implementation

### 3.1 Setup MSBuild Locator

- [ ] In `Program.cs`, add at the VERY BEGINNING of `Main` method (before any other code):
  ```csharp
  // Initialize MSBuild - MUST be first!
  if (!Microsoft.Build.Locator.MSBuildLocator.IsRegistered)
  {
      VisualStudioInstance[] instances = Microsoft.Build.Locator.MSBuildLocator
          .QueryVisualStudioInstances()
          .ToArray();

      if (instances.Length > 0)
      {
          // Use the latest version
          VisualStudioInstance latest = instances.OrderByDescending(i => i.Version).First();
          Microsoft.Build.Locator.MSBuildLocator.RegisterInstance(latest);
          Console.WriteLine($"Using MSBuild from: {latest.MSBuildPath}");
      }
      else
      {
          Console.WriteLine("WARNING: No MSBuild instance found. Code analysis may not work.");
      }
  }
  ```

- [ ] **IMPORTANT:** This MUST be before `WebApplication.CreateBuilder(args)` or any other code

### 3.2 Create Workspace Loader

Create folder structure: `MyApp.CodeAnalysis/Infrastructure/Roslyn/`

- [ ] Create `WorkspaceLoader.cs`:
  - Reference: `Resources/RoslynExamples/WorkspaceLoader.cs`
  - Methods:
    - `Task<Solution> LoadSolutionAsync(string solutionPath, CancellationToken ct)`
    - `Task<Project> LoadProjectAsync(string projectPath, CancellationToken ct)`
    - `static IEnumerable<string> FindSolutionFiles(string directoryPath)`
    - `static IEnumerable<string> FindProjectFiles(string directoryPath)`
  - Handle workspace failures by logging diagnostics
  - Implement `IDisposable` to clean up workspace

### 3.3 Create Symbol Declaration Collector

- [ ] Create `SymbolDeclarationCollector.cs`:
  - Reference: `Resources/RoslynExamples/SymbolDeclarationCollector.cs`
  - Inherit from `CSharpSyntaxWalker`
  - Constructor parameters: `SemanticModel`, `long fileId`, `long snapshotId`, `ICodeGraphRepository`
  - Override `Visit*` methods for:
    - `VisitNamespaceDeclaration`
    - `VisitFileScopedNamespaceDeclaration`
    - `VisitClassDeclaration`
    - `VisitInterfaceDeclaration`
    - `VisitStructDeclaration`
    - `VisitRecordDeclaration`
    - `VisitEnumDeclaration`
    - `VisitMethodDeclaration`
    - `VisitConstructorDeclaration`
    - `VisitPropertyDeclaration`
    - `VisitFieldDeclaration`
    - `VisitEventDeclaration`
    - `VisitDelegateDeclaration`
    - `VisitEnumMemberDeclaration`
  - Track containment hierarchy using a `Stack<long>` (parent node IDs)
  - Record inheritance and interface implementation edges
  - **Capture additional metadata:**
    - `IsExtensionMethod` (from IMethodSymbol)
    - `IsAsync` (from IMethodSymbol)
    - `ContainingType` for nested types (via ParentNodeId)

### 3.4 Create Reference Collector

- [ ] Create `ReferenceCollector.cs`:
  - Reference: `Resources/RoslynExamples/ReferenceCollector.cs`
  - Inherit from `CSharpSyntaxWalker`
  - Track current context (which method we're inside) using a `Stack<long>`
  - Override `Visit*` methods for:
    - `VisitInvocationExpression` (method calls)
    - `VisitObjectCreationExpression` (new T())
    - `VisitImplicitObjectCreationExpression` (new())
    - `VisitMemberAccessExpression` (property/field access)
    - `VisitVariableDeclaration` (type usage)
    - `VisitParameter` (parameter type usage)
    - `VisitCastExpression` (casts)
    - `VisitThrowStatement` (throw)
    - `VisitCatchDeclaration` (catch)
    - `VisitTypeArgumentList` (generic arguments)
    - `VisitAttribute` (attribute usage)
  - **Handle external symbols:**
    - When a reference target is from an external assembly (not in source)
    - Call `RecordExternalNode` to create a stub node for the external symbol
    - Create edge pointing to the external node

### 3.5 Create Main Indexer Service

- [ ] Create `RoslynCodeIndexer.cs`:
  - Implement `ICodeIndexer`
  - Inject `ICodeGraphRepository`, `ILogger<RoslynCodeIndexer>`, `IOptions<CodeAnalysisOptions>`
  - Implement `IndexSolutionAsync`:
    1. Create MSBuildWorkspace
    2. Open solution
    3. Begin transaction
    4. For each project, call `IndexProjectInternalAsync`
    5. Commit transaction
    6. Return results with timing
  - Implement `IndexProjectInternalAsync`:
    1. Get compilation
    2. **Handle partial compilation** - if compilation has errors, log them but continue
    3. **First pass**: For each document, run `SymbolDeclarationCollector`
    4. **Second pass**: For each document, run `ReferenceCollector`
    5. Track progress for status reporting
  - Implement `IndexProjectAsync` (single project)
  - Implement `IndexFileAsync` (single file - for incremental updates)
  - **Add logging throughout:**
    - Log indexing start/end with timing
    - Log number of symbols/references collected
    - Log errors during analysis (but don't fail on non-fatal errors)

### 3.6 Create Background Job Service

- [ ] Create `IndexingJobService.cs` in `MyApp.CodeAnalysis/Infrastructure/Jobs/`:
  - Implement `IIndexingJobService`
  - Use a `ConcurrentDictionary<string, IndexingJobState>` for tracking job state
  - Inject `IServiceScopeFactory` to create scoped services for background work
  - Implement `QueueIndexingAsync`:
    1. Check if job already running for this repository
    2. Create repository snapshot with Status = Queued
    3. Start background task using `Task.Run` (or use hosted service pattern)
    4. Update status to Running when started
    5. Call indexer
    6. Update status to Completed or Failed when done
  - Implement `GetJobStatusAsync`:
    - Return current status including progress
  - Implement `CancelJobAsync`:
    - Use CancellationTokenSource to signal cancellation

- [ ] **Alternative (more robust):** Use a BackgroundService pattern:
  - Create `IndexingBackgroundService : BackgroundService`
  - Use a Channel<IndexingJob> for job queue
  - Process jobs sequentially (or with limited concurrency)
  - Store job state in database (not just memory)

### 3.7 Register Indexer Services

- [ ] In `Program.cs`, add:
  ```csharp
  builder.Services.AddScoped<ICodeIndexer, RoslynCodeIndexer>();
  builder.Services.AddSingleton<IIndexingJobService, IndexingJobService>();

  // If using BackgroundService pattern:
  // builder.Services.AddHostedService<IndexingBackgroundService>();
  ```

### 3.8 Verification

- [ ] Write integration test that:
  - Points to a small test project (you can use MyApp itself)
  - Runs the indexer
  - Verifies nodes were created
  - Verifies edges were created
  - Verifies source locations were recorded

- [ ] Test with edge cases:
  - Empty project
  - Project with compilation errors (should still index what it can - **partial success**)
  - Project with multiple files
  - **Test MSBuildLocator failure** (no SDK installed scenario)

---

## Phase 4: API Endpoints

### 4.1 Create API Controller

Create folder: `MyApp/Controllers/Api/` (if not exists)

- [ ] Create `CodeAnalysisApiController.cs`:
  - Attribute: `[ApiController]`, `[Route("api/code-analysis")]`
  - Inject: `IIndexingJobService`, `ICodeGraphRepository`, `ILogger<CodeAnalysisApiController>`
  - **Note:** Do NOT inject `ICodeIndexer` directly - use job service for background processing

### 4.2 Implement Endpoints

- [ ] `POST /api/code-analysis/index`:
  - Request body: `{ repositoryId: string }` (NOT repositoryPath - security!)
  - **Validate repositoryId** exists in your system (use existing repository storage)
  - Queue indexing job (returns immediately)
  - Return: `{ jobId: long, status: "Queued", message: "Indexing started" }`
  - **Security:** Validate repositoryId against known repositories to prevent path traversal

- [ ] `GET /api/code-analysis/status`:
  - Query params: `repositoryId` (required)
  - Return: `{ repositoryId, status, startedAt, completedAt, filesIndexed, totalFiles, currentFile, errorMessage, progressPercent }`
  - This drives the UI status indicator

- [ ] `GET /api/code-analysis/graph`:
  - Query params:
    - `repositoryId` (required)
    - `maxDepth` (default 2)
    - `maxNodes` (default 100)
    - `maxEdges` (default 500)
    - `includeMembers` (default false)
    - `rootNodeId` (optional - focus on specific node)
    - `namespaceFilter` (optional)
    - `symbolKinds` (optional, comma-separated)
  - Call `GetGraphDataAsync` on repository
  - Return: `{ nodes: [...], edges: [...], hasMore: bool }`

- [ ] `GET /api/code-analysis/symbols/{symbolId}/references`:
  - Get all locations where a symbol is referenced
  - Return: `[{ filePath, line, column, endLine, endColumn, context }, ...]`

- [ ] `GET /api/code-analysis/symbols/{symbolId}/callers`:
  - Get all methods that call this method
  - Query edges where `type = Call` and `targetNodeId = symbolId`
  - Return: `[{ id, displayName, kind, filePath, line }, ...]`

- [ ] `GET /api/code-analysis/symbols/{symbolId}/callees`:
  - Get all methods that this method calls
  - Query edges where `type = Call` and `sourceNodeId = symbolId`
  - Return: `[{ id, displayName, kind, filePath, line }, ...]`

- [ ] `GET /api/code-analysis/inheritance/{symbolId}`:
  - Query params: `ancestors` (default true), `descendants` (default true)
  - Build inheritance tree
  - Return: `{ ancestors: [...], descendants: [...] }`

- [ ] `GET /api/code-analysis/search`:
  - Query params: `repositoryId`, `query`, `limit` (default 20)
  - Search by display name using NormalizedName column
  - Return: `[{ id, displayName, serializedName, kind, filePath, line }, ...]`

- [ ] `GET /api/code-analysis/source`:
  - Query params: `repositoryId`, `filePath`, `startLine`, `endLine` (default: entire file)
  - **Security:** Validate filePath is within repository root
  - Read source code from repository (use existing LocalRepository storage)
  - Return: `{ content: string, language: "csharp" }`

### 4.3 Add Swagger Documentation

- [ ] Add `[SwaggerOperation]` attributes to each endpoint
- [ ] Add `[ProducesResponseType]` attributes for response types
- [ ] Add XML comments for parameters
- [ ] Document error responses (404, 400, 500)

### 4.4 Verification

- [ ] Test each endpoint using Swagger UI
- [ ] Verify correct HTTP status codes (200, 404, 400, 500)
- [ ] Verify error handling:
  - Invalid repositoryId → 404
  - Symbol not found → 404
  - Invalid path (traversal attempt) → 400
  - MSBuild not available → 500 with helpful message
- [ ] **Security tests:**
  - Test path traversal attempts (e.g., `../../../etc/passwd`)
  - Test accessing repositories not owned by the user

---

## Phase 5: Frontend Graph Visualization

### 5.1 Add Cytoscape.js Libraries

- [ ] Download or reference Cytoscape.js libraries:
  ```html
  <!-- Add to _Layout.cshtml or specific view -->
  <script src="https://unpkg.com/cytoscape@3.28.1/dist/cytoscape.min.js"></script>
  <script src="https://unpkg.com/dagre@0.8.5/dist/dagre.min.js"></script>
  <script src="https://unpkg.com/cytoscape-dagre@2.5.0/cytoscape-dagre.js"></script>
  ```

- [ ] Alternatively, install locally:
  ```bash
  cd MyApp/wwwroot/lib
  mkdir cytoscape
  # Download files from unpkg and place in cytoscape folder
  ```

- [ ] Add syntax highlighting library (for source preview):
  ```html
  <link href="https://unpkg.com/prismjs@1.29.0/themes/prism-tomorrow.css" rel="stylesheet" />
  <script src="https://unpkg.com/prismjs@1.29.0/prism.js"></script>
  <script src="https://unpkg.com/prismjs@1.29.0/components/prism-csharp.js"></script>
  ```

### 5.2 Create Graph Renderer JavaScript

- [ ] Create folder: `MyApp/wwwroot/js/code-analysis/`

- [ ] Create `code-graph-renderer.js`:
  - Reference: `Resources/CytoscapeExamples/graph-renderer.js`
  - Adapt styling to match the Flow app's dark theme
  - **Follow site.js patterns:**
    - DOM-rooted initialization
    - `data-` attributes for component discovery
    - Event delegation
    - Custom events for inter-component communication
  - Implement `CodeGraphRenderer` class with:
    - `constructor(containerId, options)`
    - `async initialize()`
    - `getStyles()` - return Cytoscape style array
    - `setupEventHandlers()` - click, double-click, hover handlers
    - `async loadGraph(repositoryId, options)`
    - `transformToElements(graphData)`
    - `applyLayout(layoutName)` - dagre, cose, breadthfirst, circle
    - `focusOnNode(nodeId)`
    - `highlightPath(sourceId, targetId)`
    - `filterByKind(kinds)`
    - `search(query)`
    - `exportAsImage(format)`
    - `destroy()`
  - **Emit custom events:**
    - `codeGraph:nodeSelected` - when user clicks a node
    - `codeGraph:navigateToSource` - when user double-clicks
    - `codeGraph:graphLoaded` - when graph data is loaded
    - `codeGraph:filterChanged` - when filters are applied

### 5.3 Create Controller and View

- [ ] Create `CodeAnalysisController.cs` (MVC controller, NOT API):
  - Location: `MyApp/Controllers/CodeAnalysisController.cs`
  - Action: `Index(string? repositoryId)` - returns the main view
  - **Note:** Separate from Flowcharts to avoid coupling

- [ ] Create view folder: `MyApp/Views/CodeAnalysis/`

- [ ] Create `Index.cshtml`:
  - Layout: Three-column layout (symbol tree | graph canvas | details panel)
  - Include:
    - Left sidebar: Symbol search input, filter checkboxes, symbol tree (collapsed by default)
    - Center: Toolbar (layout selector, fit, export), graph container, loading indicator
    - Right sidebar: Symbol details, source preview, references list
  - Use Tailwind classes matching the existing dark theme
  - **Add status indicator:**
    - Show indexing status (Queued, Running, Complete, Failed)
    - Show last indexed commit
    - Show progress during indexing

### 5.4 Create Partial Views

- [ ] Create `_SymbolDetails.cshtml`:
  - Display symbol name, kind (with icon), full name, location
  - Include "View in File" link
  - Show modifiers (static, abstract, virtual, async)

- [ ] Create `_SourcePreview.cshtml`:
  - Code block with Prism.js syntax highlighting
  - Line numbers
  - Highlight current line
  - Support for scrolling to specific line

- [ ] Create `_ReferencesList.cshtml`:
  - List of file:line references
  - Clickable to navigate
  - Group by file for better readability

- [ ] Create `_FilterPanel.cshtml`:
  - Checkboxes for symbol kinds (Classes, Interfaces, Methods, Properties, Fields)
  - Namespace filter input with autocomplete
  - Depth slider (1-5)
  - "Show only selected node's neighbors" toggle

### 5.5 Wire Up JavaScript Events

In the view's `@section Scripts`:

- [ ] Initialize graph renderer on page load (follow site.js initialization pattern)
- [ ] Handle layout selector change
- [ ] Handle fit-to-view button
- [ ] Handle export button
- [ ] Handle `codeGraph:nodeSelected` event - update details panel
- [ ] Handle `codeGraph:navigateToSource` event - load source preview
- [ ] Implement symbol search functionality with debounce
- [ ] Poll for indexing status if status != Complete
- [ ] **Error handling:** Show user-friendly messages for network errors

### 5.6 Add Navigation Link

- [ ] Add "Code Analysis" link to the sidebar navigation in `_Layout.cshtml`
- [ ] Add appropriate icon (use existing icon style, e.g., graph/nodes icon)
- [ ] Position appropriately in the navigation hierarchy

### 5.7 Verification

- [ ] Load the page and verify Cytoscape initializes without errors
- [ ] Index a repository and verify:
  - Status indicator shows progress
  - Graph loads when indexing completes
  - Nodes appear with correct styling
- [ ] Test all layout options
- [ ] Test node selection and details display
- [ ] Test source code preview with syntax highlighting
- [ ] Test export functionality (PNG/SVG)
- [ ] Test on different screen sizes (responsive design)
- [ ] Test filter panel functionality
- [ ] **Performance:** Verify UI remains responsive with 100+ nodes

---

## Phase 6: Integration and Polish

### 6.1 Connect Repository Selection to Indexing

- [ ] On the Home page (repository list), add an "Analyze" button for each repository
- [ ] Clicking "Analyze" should:
  1. Navigate to Code Analysis page with repositoryId
  2. Check if repository is already indexed (GET /status)
  3. If not indexed or outdated, show "Start Indexing" button
  4. When indexing, show progress indicator
  5. Load graph when complete

- [ ] Add indexing status indicator to repository cards:
  - "Not indexed" / "Indexed" / "Indexing..." / "Failed"
  - Show last indexed date/commit

### 6.2 Implement Incremental Indexing

- [ ] Track file modification times and hashes
- [ ] On re-index:
  1. Check each file's hash against stored hash
  2. Only process changed files
  3. Remove nodes/edges for deleted files
  4. Update existing nodes/edges rather than recreating
- [ ] Add "Force Re-index" option for full rebuild

### 6.3 Add Graph Filtering UI

- [ ] Add filter checkboxes for symbol kinds (already in Phase 5)
- [ ] Add namespace filter with autocomplete (already in Phase 5)
- [ ] Implement "Show only selected node's neighbors" mode:
  - When enabled, clicking a node hides all non-connected nodes
  - Double-click to expand neighbors

### 6.4 Add Path Highlighting

- [ ] Add "Find path between" mode:
  1. User clicks first node (show "Select second node" prompt)
  2. User clicks second node
  3. Calculate shortest path (BFS on edges)
  4. Highlight path edges and nodes
  5. Click anywhere to clear

### 6.5 Performance Optimization

- [ ] Add pagination for large graphs:
  - Load visible area + buffer
  - Use "Load more" button or scroll pagination
- [ ] Implement level-of-detail:
  - Hide member-level nodes when zoomed out
  - Show only classes/interfaces at high zoom
- [ ] Check query performance:
  - Run EXPLAIN QUERY PLAN on slow queries
  - Add indices if needed
  - Consider caching frequent queries
- [ ] **Benchmark:**
  - Document indexing time for different project sizes
  - Document graph loading time
  - Target: < 30s indexing for medium project, < 2s graph load

### 6.6 Error Handling and Logging

- [ ] Add user-friendly error messages for:
  - Indexing failures (MSBuild not found, compilation errors)
  - Network errors when loading graph
  - Invalid repository (not found, no access)

- [ ] Add Serilog logging throughout:
  ```csharp
  _logger.LogInformation("Starting indexing for repository {RepositoryId}", repositoryId);
  _logger.LogInformation("Collected {SymbolCount} symbols in {Duration}ms", count, duration);
  _logger.LogError(ex, "Error indexing file {FilePath}", filePath);
  ```

### 6.7 Testing

- [ ] **Unit Tests:**
  - `CSharpSymbolKind` enum values match schema
  - `CSharpReferenceKind` enum values match schema
  - `CodeGraphRepository.RecordNode` creates and caches correctly
  - `CodeGraphRepository.RecordEdge` creates relationships correctly
  - `CodeGraphRepository.GetGraphDataAsync` respects limits

- [ ] **Integration Tests:**
  - `WorkspaceLoader` successfully loads a .sln file
  - `WorkspaceLoader` successfully loads a .csproj file
  - `SymbolDeclarationCollector` collects all symbol types
  - `ReferenceCollector` collects all reference types
  - `RoslynCodeIndexer` completes full indexing cycle
  - API endpoints return correct data
  - **Security tests:**
    - Path traversal prevention
    - RepositoryId validation

- [ ] **End-to-End Tests:**
  - Full flow from repository selection to graph visualization
  - Source code navigation works correctly
  - Search returns relevant results
  - Graph layouts render correctly
  - Export produces valid image

- [ ] **Consistency Checks:**
  - Occurrences always have valid SourceLocation
  - Edges always point to existing nodes
  - No orphan nodes (unless external symbols)

### 6.8 Final Verification

- [ ] Test complete flow:
  1. Select a repository from the list
  2. Click Analyze
  3. Wait for indexing (observe progress)
  4. View graph
  5. Navigate to source
  6. Search symbols
  7. Filter by type
  8. Export image

- [ ] Test with:
  - Small project (few files)
  - Medium project (MyApp itself)
  - Edge cases (empty project, compilation errors)

- [ ] Performance testing:
  - Measure indexing time for different project sizes
  - Measure graph loading time
  - Ensure UI remains responsive
  - Document results for baseline

---

## Common Issues and Solutions

### MSBuild Locator

**Issue:** `InvalidOperationException: No instances of MSBuild found`
**Solution:**
- Ensure MSBuildLocator.RegisterDefaults() is called FIRST in Main()
- Ensure .NET SDK is installed
- Check that the application has access to the SDK path

### Workspace Loading

**Issue:** Projects fail to load with "Could not load file or assembly"
**Solution:**
- Ensure all project dependencies are restored (`dotnet restore`)
- Check that target framework is compatible
- Try building the project first to ensure it compiles

### Roslyn Analysis

**Issue:** SemanticModel returns null for symbols
**Solution:**
- Ensure compilation is complete
- Check for compilation errors
- Some symbols may be in external assemblies (handle with RecordExternalNode)

### Cytoscape Performance

**Issue:** Graph is slow with many nodes
**Solution:**
- Limit initial load (maxNodes/maxEdges)
- Use pagination
- Enable WebGL renderer (for very large graphs)
- Reduce detail at low zoom levels

### Database Performance

**Issue:** Queries are slow
**Solution:**
- Add indices (check Phase 2.2 for required indices)
- Use `.AsNoTracking()` for read-only queries
- Consider batching writes during indexing
- Run `EXPLAIN QUERY PLAN` on slow queries

### Path Traversal Security

**Issue:** User attempts to access files outside repository
**Solution:**
- Always validate repositoryId against known repositories
- Never accept raw file paths from user input
- Use Path.GetFullPath() and check result is under repository root

---

## Resources Reference

- `Resources/README.md` - Overview of all resources
- `Resources/RoslynExamples/` - C# code examples
- `Resources/CytoscapeExamples/` - JavaScript examples
- `Resources/SourcetrailReference/` - Database schema
- `code_analysis_integration_plan.md` - Detailed implementation plan

---

## Sign-off

When complete, verify:

- [ ] All checklist items are done
- [ ] All tests pass
- [ ] No compiler warnings
- [ ] Code follows guidelines (no `var`, PascalCase, etc.)
- [ ] Feature works end-to-end
- [ ] Security tests pass (path traversal, repositoryId validation)
- [ ] Performance benchmarks documented
