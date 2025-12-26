# Resources for Code Analysis Integration

This folder contains **complete reference implementations** for integrating Roslyn-based code analysis and graph visualization. Each file is a working example that can be adapted to the project.

## Recent Updates (Peer Review Improvements)

The schema and checklist have been updated based on peer review feedback to include:

- **Multi-repository support**: `IndexedRepository` table with `RepositoryId` and `CommitSha`
- **Incremental indexing**: `FileHash` field for detecting changed files
- **Background job processing**: `IIndexingJobService` for async indexing with status tracking
- **Enhanced symbol metadata**: `IsExtensionMethod`, `IsAsync`, `ParentNodeId` for containment hierarchy
- **Precise navigation**: `StartOffset`/`EndOffset` byte offsets in `SourceLocation`
- **Efficient search**: `NormalizedName` (lowercase) for case-insensitive LIKE queries
- **API pagination**: `MaxNodes`/`MaxEdges` limits with `HasMore` flag
- **Security**: `repositoryId` instead of `repositoryPath` to prevent path traversal
- **Long IDs**: Using `long` instead of `int` for node/edge IDs to handle large repositories

## Folder Structure

```
Resources/
├── README.md                              # This file
├── RoslynExamples/                        # C# Roslyn implementation examples
│   ├── SymbolDeclarationCollector.cs      # First-pass: collects symbol definitions
│   ├── ReferenceCollector.cs              # Second-pass: collects usages/references
│   └── WorkspaceLoader.cs                 # MSBuild workspace loading (.sln/.csproj)
├── EFCoreExamples/                        # Database layer examples
│   └── CodeAnalysisDbContext.cs           # Complete DbContext with all entity configs
├── BackgroundJobExamples/                 # Background processing examples
│   └── IndexingBackgroundService.cs       # BackgroundService for async indexing
├── ApiExamples/                           # API controller examples
│   └── CodeAnalysisApiController.cs       # Complete controller with security validation
├── UtilityExamples/                       # Helper utilities
│   └── FileHashUtility.cs                 # SHA256 hashing for incremental indexing
├── ViewExamples/                          # Razor view examples
│   └── CodeAnalysisIndex.cshtml           # Complete three-column layout with JS
├── CytoscapeExamples/                     # JavaScript graph visualization
│   └── graph-renderer.js                  # Cytoscape.js wrapper class
└── SourcetrailReference/                  # Database schema reference
    └── DatabaseSchema.sql                 # SQLite schema with all indices
```

## Implementation Order

Follow this order for implementation:

### Phase 1: Domain & Database
1. **Read `SourcetrailReference/DatabaseSchema.sql`** - Understand the data model
2. **Use `EFCoreExamples/CodeAnalysisDbContext.cs`** - Create entities and DbContext
3. **Add migrations** - Generate database schema

### Phase 2: Roslyn Indexer
1. **Read `RoslynExamples/WorkspaceLoader.cs`** - Load .sln/.csproj files
2. **Use `RoslynExamples/SymbolDeclarationCollector.cs`** - First-pass symbol collection
3. **Use `RoslynExamples/ReferenceCollector.cs`** - Second-pass reference collection
4. **Use `UtilityExamples/FileHashUtility.cs`** - Incremental indexing support

### Phase 3: Background Processing
1. **Use `BackgroundJobExamples/IndexingBackgroundService.cs`** - Non-blocking indexing

### Phase 4: API Layer
1. **Use `ApiExamples/CodeAnalysisApiController.cs`** - All endpoints with security

### Phase 5: Frontend
1. **Use `CytoscapeExamples/graph-renderer.js`** - Graph visualization
2. **Use `ViewExamples/CodeAnalysisIndex.cshtml`** - Complete UI

## Key Roslyn Concepts

```csharp
// SyntaxTree - The parsed structure of a source file
SyntaxTree tree = await document.GetSyntaxTreeAsync();

// SemanticModel - Provides meaning (what types things are)
SemanticModel model = compilation.GetSemanticModel(tree);

// ISymbol - Represents a named entity (class, method, etc.)
ISymbol symbol = model.GetDeclaredSymbol(node);

// CSharpSyntaxWalker - Visit all nodes in a syntax tree
public class MyCollector : CSharpSyntaxWalker
{
    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // Process class declaration
        base.VisitClassDeclaration(node);
    }
}
```

## Key EF Core Concepts

```csharp
// DbContext configuration
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Configure indices (CRITICAL for performance)
    modelBuilder.Entity<CodeNode>()
        .HasIndex(n => n.SerializedName);

    // Configure relationships
    modelBuilder.Entity<CodeEdge>()
        .HasOne(e => e.SourceNode)
        .WithMany(n => n.OutgoingEdges)
        .HasForeignKey(e => e.SourceNodeId);
}
```

## Key Cytoscape.js Concepts

```javascript
// Initialize Cytoscape
const cy = cytoscape({
    container: document.getElementById('graph'),
    style: [...],  // Node/edge styles
    elements: [    // Nodes and edges
        { data: { id: 'n1', label: 'MyClass' } },
        { data: { source: 'n1', target: 'n2' } }
    ]
});

// Apply layout
cy.layout({ name: 'dagre' }).run();

// Handle events
cy.on('tap', 'node', (e) => console.log(e.target.data()));
```

## Required NuGet Packages

```xml
<ItemGroup>
  <!-- Roslyn workspaces -->
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.8.0" />
  <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.8.0" />
  <PackageReference Include="Microsoft.Build.Locator" Version="1.6.10" />

  <!-- EF Core for SQLite -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0" />
</ItemGroup>
```

## Required JavaScript Libraries

```html
<!-- Cytoscape.js and extensions -->
<script src="https://unpkg.com/cytoscape@3.28.1/dist/cytoscape.min.js"></script>
<script src="https://unpkg.com/dagre@0.8.5/dist/dagre.min.js"></script>
<script src="https://unpkg.com/cytoscape-dagre@2.5.0/cytoscape-dagre.js"></script>

<!-- Syntax highlighting (optional) -->
<link href="https://unpkg.com/prismjs@1.29.0/themes/prism-tomorrow.css" rel="stylesheet" />
<script src="https://unpkg.com/prismjs@1.29.0/prism.js"></script>
<script src="https://unpkg.com/prismjs@1.29.0/components/prism-csharp.js"></script>
```

## Security Checklist

- [ ] **NEVER** accept raw file paths from user input - use repositoryId
- [ ] Validate repositoryId against known repositories in your system
- [ ] Use `Path.GetFullPath()` and verify paths are within repository root
- [ ] Enforce pagination limits (maxNodes, maxEdges)
- [ ] Log security-relevant events (path traversal attempts)

## External Documentation Links

### Roslyn
- [Roslyn GitHub Repository](https://github.com/dotnet/roslyn)
- [Roslyn Syntax API Tutorial](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-analysis)
- [Roslyn Semantic API Tutorial](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/semantic-analysis)

### Cytoscape.js
- [Cytoscape.js Documentation](https://js.cytoscape.org/)
- [Cytoscape.js Demos](https://js.cytoscape.org/#demos)
- [Dagre Layout Extension](https://github.com/cytoscape/cytoscape.js-dagre)

### Sourcetrail Reference
- [Sourcetrail Repository (maintained fork)](https://github.com/petermost/Sourcetrail)
- [SourcetrailDB Library](https://github.com/CoatiSoftware/SourcetrailDB)
- [SourcetrailDotnetIndexer](https://github.com/packdat/SourcetrailDotnetIndexer)

## Implementation Notes

1. **MSBuildLocator MUST be initialized FIRST** in Program.cs before any other code
2. **Two-pass indexing** is essential: declarations first, then references
3. **Use transactions** when recording nodes/edges for consistency
4. **Cache node IDs** during indexing for performance (Dictionary<string, long>)
5. **Handle partial compilation** - index what's available even with errors
6. **External symbols** (System.*, etc.) should be stored as "external nodes" with minimal data
