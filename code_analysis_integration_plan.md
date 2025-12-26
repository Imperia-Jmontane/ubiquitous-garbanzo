  Detailed Implementation Plan: Integrating Sourcetrail-like Code Analysis with Roslyn for C# Projects

  Executive Summary

  Based on my analysis of:
  1. Your Flow codebase - An ASP.NET Core 9.0 MVC web application with repository management, GitHub integration, and a modern
  Tailwind CSS frontend
  2. Sourcetrail (petermost/Sourcetrail) - A C++ cross-platform source code explorer using Qt for visualization and SQLite for
  storage
  3. SourcetrailDotnetIndexer - A reflection-based .NET indexer (NOT Roslyn-based) that analyzes compiled assemblies

  I'll outline how to build a Roslyn-based C# code analysis system with a custom graph visualization integrated into your webapp.

  ---
  Part 1: Architecture Overview

  1.1 Key Insight from Sourcetrail

  Sourcetrail's architecture separates concerns into:
  Source Code → Indexer (Language-specific) → SQLite Database → Graph Visualization

  The SourcetrailDB schema provides the foundation:

  | Table           | Purpose                                                   |
  |-----------------|-----------------------------------------------------------|
  | node            | Code symbols (classes, methods, properties, fields, etc.) |
  | edge            | Relationships between nodes (calls, inherits, uses, etc.) |
  | file            | Source files with language metadata                       |
  | source_location | Line/column positions in source files                     |
  | occurrence      | Links elements to their source locations                  |
  | symbol          | Definition kind (explicit/implicit)                       |
  | local_symbol    | Scoped symbols within methods                             |

  1.2 Why Build with Roslyn (Not Reflection)

  The existing SourcetrailDotnetIndexer uses IL reflection on compiled assemblies:
  - ✗ Requires compilation first
  - ✗ Limited source location information (depends on PDB files)
  - ✗ Cannot analyze incomplete/in-progress code
  - ✗ Misses compile-time constructs like using directives, comments

  Roslyn advantages:
  - ✓ Analyzes source code directly
  - ✓ Full semantic model with precise source locations
  - ✓ Works on incomplete/uncompilable code
  - ✓ Access to syntax trees for richer analysis
  - ✓ Incremental analysis capabilities

  ---
  Part 2: Database Schema Design

  2.1 Adapted Schema for C# Analysis

  Create a new SQLite database schema adapted from SourcetrailDB but optimized for C#:

  -- Core metadata
  CREATE TABLE meta (
      id INTEGER PRIMARY KEY,
      key TEXT NOT NULL,
      value TEXT
  );

  -- All indexable elements share this base
  CREATE TABLE element (
      id INTEGER PRIMARY KEY AUTOINCREMENT
  );

  -- Code symbols (namespaces, types, members)
  CREATE TABLE node (
      id INTEGER PRIMARY KEY,
      type INTEGER NOT NULL,        -- SymbolKind enum
      serialized_name TEXT NOT NULL,
      display_name TEXT,
      accessibility INTEGER,        -- public/private/protected/internal
      is_static INTEGER DEFAULT 0,
      is_abstract INTEGER DEFAULT 0,
      is_virtual INTEGER DEFAULT 0,
      is_override INTEGER DEFAULT 0,
      FOREIGN KEY(id) REFERENCES element(id) ON DELETE CASCADE
  );

  -- Relationships between nodes
  CREATE TABLE edge (
      id INTEGER PRIMARY KEY,
      type INTEGER NOT NULL,        -- ReferenceKind enum
      source_node_id INTEGER NOT NULL,
      target_node_id INTEGER NOT NULL,
      FOREIGN KEY(id) REFERENCES element(id) ON DELETE CASCADE,
      FOREIGN KEY(source_node_id) REFERENCES node(id) ON DELETE CASCADE,
      FOREIGN KEY(target_node_id) REFERENCES node(id) ON DELETE CASCADE
  );

  -- Source files
  CREATE TABLE file (
      id INTEGER PRIMARY KEY,
      path TEXT NOT NULL UNIQUE,
      language TEXT DEFAULT 'csharp',
      modification_time TEXT,
      indexed INTEGER DEFAULT 0,
      line_count INTEGER,
      FOREIGN KEY(id) REFERENCES node(id) ON DELETE CASCADE
  );

  -- Source locations for clickable navigation
  CREATE TABLE source_location (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      file_node_id INTEGER NOT NULL,
      start_line INTEGER NOT NULL,
      start_column INTEGER NOT NULL,
      end_line INTEGER NOT NULL,
      end_column INTEGER NOT NULL,
      location_type INTEGER DEFAULT 0,  -- definition, reference, scope
      FOREIGN KEY(file_node_id) REFERENCES file(id) ON DELETE CASCADE
  );

  -- Links elements to their source locations
  CREATE TABLE occurrence (
      element_id INTEGER NOT NULL,
      source_location_id INTEGER NOT NULL,
      PRIMARY KEY(element_id, source_location_id),
      FOREIGN KEY(element_id) REFERENCES element(id) ON DELETE CASCADE,
      FOREIGN KEY(source_location_id) REFERENCES source_location(id) ON DELETE CASCADE
  );

  -- Symbol definition metadata
  CREATE TABLE symbol (
      id INTEGER PRIMARY KEY,
      definition_kind INTEGER NOT NULL,  -- explicit, implicit
      FOREIGN KEY(id) REFERENCES node(id) ON DELETE CASCADE
  );

  -- Local variables and parameters
  CREATE TABLE local_symbol (
      id INTEGER PRIMARY KEY,
      name TEXT NOT NULL,
      containing_method_id INTEGER,
      FOREIGN KEY(id) REFERENCES element(id) ON DELETE CASCADE,
      FOREIGN KEY(containing_method_id) REFERENCES node(id) ON DELETE CASCADE
  );

  -- Indexing errors
  CREATE TABLE error (
      id INTEGER PRIMARY KEY,
      message TEXT,
      fatal INTEGER DEFAULT 0,
      file_id INTEGER,
      line INTEGER,
      column INTEGER,
      FOREIGN KEY(id) REFERENCES element(id) ON DELETE CASCADE,
      FOREIGN KEY(file_id) REFERENCES file(id) ON DELETE CASCADE
  );

  -- Indices for performance
  CREATE INDEX idx_node_name ON node(serialized_name);
  CREATE INDEX idx_node_type ON node(type);
  CREATE INDEX idx_edge_source ON edge(source_node_id);
  CREATE INDEX idx_edge_target ON edge(target_node_id);
  CREATE INDEX idx_edge_type ON edge(type);
  CREATE INDEX idx_source_location_file ON source_location(file_node_id);
  CREATE INDEX idx_occurrence_element ON occurrence(element_id);

  2.2 C# Symbol Types (SymbolKind Enum)

  public enum CSharpSymbolKind
  {
      Unknown = 0,

      // Structural
      Namespace = 1,
      Assembly = 2,
      Module = 3,

      // Types
      Class = 10,
      Struct = 11,
      Interface = 12,
      Enum = 13,
      Delegate = 14,
      Record = 15,
      RecordStruct = 16,

      // Type Members
      Field = 20,
      Property = 21,
      Method = 22,
      Constructor = 23,
      Destructor = 24,
      Operator = 25,
      Indexer = 26,
      Event = 27,
      EnumMember = 28,

      // Local
      LocalVariable = 30,
      Parameter = 31,
      TypeParameter = 32,

      // Other
      File = 50,
      Using = 51,
      Attribute = 52
  }

  2.3 Reference Types (ReferenceKind Enum)

  public enum CSharpReferenceKind
  {
      Unknown = 0,

      // Type relationships
      Inheritance = 1,          // class : BaseClass
      InterfaceImplementation = 2,  // class : IInterface

      // Usage relationships
      Call = 10,                // method invocation
      TypeUsage = 11,           // variable declaration type
      Override = 12,            // method override

      // Member access
      FieldAccess = 20,
      PropertyAccess = 21,
      EventAccess = 22,

      // Containment
      Contains = 30,            // namespace contains class

      // Dependencies
      Import = 40,              // using directive
      TypeArgument = 41,        // generic type argument
      AttributeUsage = 42,

      // Special
      Instantiation = 50,       // new T()
      Cast = 51,
      Throw = 52,
      Catch = 53
  }

  ---
  Part 3: Roslyn-Based Indexer Implementation

  3.1 Project Structure

  MyApp.CodeAnalysis/
  ├── Domain/
  │   ├── Entities/
  │   │   ├── CodeNode.cs
  │   │   ├── CodeEdge.cs
  │   │   ├── SourceFile.cs
  │   │   └── SourceLocation.cs
  │   ├── Enums/
  │   │   ├── CSharpSymbolKind.cs
  │   │   └── CSharpReferenceKind.cs
  │   └── Services/
  │       └── ICodeIndexer.cs
  ├── Application/
  │   ├── Analysis/
  │   │   ├── CSharpSyntaxWalker.cs
  │   │   ├── SemanticModelAnalyzer.cs
  │   │   └── ReferenceCollector.cs
  │   ├── Commands/
  │   │   ├── IndexRepositoryCommand.cs
  │   │   └── IndexFileCommand.cs
  │   └── Queries/
  │       ├── GetSymbolReferencesQuery.cs
  │       ├── GetCallGraphQuery.cs
  │       └── GetInheritanceHierarchyQuery.cs
  ├── Infrastructure/
  │   ├── Persistence/
  │   │   ├── CodeAnalysisDbContext.cs
  │   │   └── CodeGraphRepository.cs
  │   └── Roslyn/
  │       ├── WorkspaceLoader.cs
  │       ├── ProjectAnalyzer.cs
  │       └── SymbolIndexer.cs
  └── Api/
      └── Controllers/
          └── CodeAnalysisController.cs

  3.2 Core Roslyn Analysis Service

  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.MSBuild;

  namespace MyApp.CodeAnalysis.Infrastructure.Roslyn;

  public interface ICodeIndexer
  {
      Task<IndexingResult> IndexSolutionAsync(string solutionPath, CancellationToken ct);
      Task<IndexingResult> IndexProjectAsync(string projectPath, CancellationToken ct);
      Task<IndexingResult> IndexFileAsync(string filePath, CancellationToken ct);
  }

  public class RoslynCodeIndexer : ICodeIndexer
  {
      private readonly ICodeGraphRepository _repository;
      private readonly ILogger<RoslynCodeIndexer> _logger;

      public RoslynCodeIndexer(
          ICodeGraphRepository repository,
          ILogger<RoslynCodeIndexer> logger)
      {
          _repository = repository;
          _logger = logger;
      }

      public async Task<IndexingResult> IndexSolutionAsync(
          string solutionPath,
          CancellationToken ct)
      {
          using MSBuildWorkspace workspace = MSBuildWorkspace.Create();

          workspace.WorkspaceFailed += (sender, args) =>
          {
              _logger.LogWarning(
                  "Workspace diagnostic: {Kind} - {Message}",
                  args.Diagnostic.Kind,
                  args.Diagnostic.Message);
          };

          Solution solution = await workspace.OpenSolutionAsync(solutionPath, ct);
          IndexingResult result = new IndexingResult();

          await _repository.BeginTransactionAsync(ct);

          try
          {
              foreach (Project project in solution.Projects)
              {
                  await IndexProjectInternalAsync(project, result, ct);
              }

              await _repository.CommitTransactionAsync(ct);
          }
          catch
          {
              await _repository.RollbackTransactionAsync(ct);
              throw;
          }

          return result;
      }

      private async Task IndexProjectInternalAsync(
          Project project,
          IndexingResult result,
          CancellationToken ct)
      {
          Compilation? compilation = await project.GetCompilationAsync(ct);

          if (compilation == null)
          {
              result.Errors.Add($"Could not compile project: {project.Name}");
              return;
          }

          // First pass: collect all symbol declarations
          foreach (Document document in project.Documents)
          {
              if (document.FilePath == null) continue;

              SyntaxTree? syntaxTree = await document.GetSyntaxTreeAsync(ct);
              SemanticModel? semanticModel = compilation.GetSemanticModel(syntaxTree!);

              int fileId = await _repository.RecordFileAsync(
                  document.FilePath,
                  "csharp",
                  ct);

              SymbolDeclarationCollector collector = new SymbolDeclarationCollector(
                  semanticModel,
                  fileId,
                  _repository);

              SyntaxNode root = await syntaxTree!.GetRootAsync(ct);
              collector.Visit(root);

              result.FilesIndexed++;
              result.SymbolsCollected += collector.SymbolCount;
          }

          // Second pass: collect all references
          foreach (Document document in project.Documents)
          {
              if (document.FilePath == null) continue;

              SyntaxTree? syntaxTree = await document.GetSyntaxTreeAsync(ct);
              SemanticModel? semanticModel = compilation.GetSemanticModel(syntaxTree!);

              int fileId = await _repository.GetFileIdAsync(document.FilePath, ct);

              ReferenceCollector refCollector = new ReferenceCollector(
                  semanticModel,
                  fileId,
                  _repository);

              SyntaxNode root = await syntaxTree!.GetRootAsync(ct);
              refCollector.Visit(root);

              result.ReferencesCollected += refCollector.ReferenceCount;
          }
      }
  }

  3.3 Symbol Declaration Collector (Syntax Walker)

  public class SymbolDeclarationCollector : CSharpSyntaxWalker
  {
      private readonly SemanticModel _semanticModel;
      private readonly int _fileId;
      private readonly ICodeGraphRepository _repository;
      private readonly Stack<int> _containerStack = new();

      public int SymbolCount { get; private set; }

      public SymbolDeclarationCollector(
          SemanticModel semanticModel,
          int fileId,
          ICodeGraphRepository repository)
      {
          _semanticModel = semanticModel;
          _fileId = fileId;
          _repository = repository;
      }

      public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
      {
          INamespaceSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);
          if (symbol != null)
          {
              int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Namespace);
              RecordContainment(nodeId);
              _containerStack.Push(nodeId);
          }

          base.VisitNamespaceDeclaration(node);

          if (symbol != null) _containerStack.Pop();
      }

      public override void VisitClassDeclaration(ClassDeclarationSyntax node)
      {
          INamedTypeSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);
          if (symbol != null)
          {
              CSharpSymbolKind kind = node.Keyword.ValueText switch
              {
                  "record" => CSharpSymbolKind.Record,
                  _ => CSharpSymbolKind.Class
              };

              int nodeId = RecordSymbol(symbol, node, kind);
              RecordContainment(nodeId);
              RecordTypeRelationships(symbol, nodeId);
              _containerStack.Push(nodeId);
          }

          base.VisitClassDeclaration(node);

          if (symbol != null) _containerStack.Pop();
      }

      public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
      {
          INamedTypeSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);
          if (symbol != null)
          {
              int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Interface);
              RecordContainment(nodeId);
              RecordTypeRelationships(symbol, nodeId);
              _containerStack.Push(nodeId);
          }

          base.VisitInterfaceDeclaration(node);

          if (symbol != null) _containerStack.Pop();
      }

      public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
      {
          IMethodSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);
          if (symbol != null)
          {
              int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Method);
              RecordContainment(nodeId);

              // Record override relationship
              if (symbol.OverriddenMethod != null)
              {
                  int targetId = _repository.GetOrCreateNodeId(
                      GetFullyQualifiedName(symbol.OverriddenMethod));
                  _repository.RecordEdge(
                      nodeId,
                      targetId,
                      CSharpReferenceKind.Override);
              }
          }

          base.VisitMethodDeclaration(node);
      }

      public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
      {
          IPropertySymbol? symbol = _semanticModel.GetDeclaredSymbol(node);
          if (symbol != null)
          {
              int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Property);
              RecordContainment(nodeId);
          }

          base.VisitPropertyDeclaration(node);
      }

      public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
      {
          foreach (VariableDeclaratorSyntax variable in node.Declaration.Variables)
          {
              IFieldSymbol? symbol = _semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
              if (symbol != null)
              {
                  int nodeId = RecordSymbol(symbol, variable, CSharpSymbolKind.Field);
                  RecordContainment(nodeId);
              }
          }

          base.VisitFieldDeclaration(node);
      }

      private int RecordSymbol(ISymbol symbol, SyntaxNode node, CSharpSymbolKind kind)
      {
          string fullyQualifiedName = GetFullyQualifiedName(symbol);
          string displayName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

          FileLinePositionSpan span = node.GetLocation().GetLineSpan();

          int nodeId = _repository.RecordNode(
              fullyQualifiedName,
              displayName,
              kind,
              GetAccessibility(symbol),
              symbol.IsStatic,
              symbol.IsAbstract,
              symbol.IsVirtual,
              symbol.IsOverride);

          _repository.RecordSourceLocation(
              nodeId,
              _fileId,
              span.StartLinePosition.Line + 1,
              span.StartLinePosition.Character + 1,
              span.EndLinePosition.Line + 1,
              span.EndLinePosition.Character + 1,
              LocationType.Definition);

          SymbolCount++;
          return nodeId;
      }

      private void RecordContainment(int childId)
      {
          if (_containerStack.Count > 0)
          {
              int containerId = _containerStack.Peek();
              _repository.RecordEdge(containerId, childId, CSharpReferenceKind.Contains);
          }
      }

      private void RecordTypeRelationships(INamedTypeSymbol typeSymbol, int nodeId)
      {
          // Base class
          if (typeSymbol.BaseType != null &&
              typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
          {
              int baseId = _repository.GetOrCreateNodeId(
                  GetFullyQualifiedName(typeSymbol.BaseType));
              _repository.RecordEdge(nodeId, baseId, CSharpReferenceKind.Inheritance);
          }

          // Implemented interfaces
          foreach (INamedTypeSymbol iface in typeSymbol.Interfaces)
          {
              int ifaceId = _repository.GetOrCreateNodeId(
                  GetFullyQualifiedName(iface));
              _repository.RecordEdge(
                  nodeId,
                  ifaceId,
                  CSharpReferenceKind.InterfaceImplementation);
          }
      }

      private string GetFullyQualifiedName(ISymbol symbol)
      {
          return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
      }

      private int GetAccessibility(ISymbol symbol)
      {
          return symbol.DeclaredAccessibility switch
          {
              Accessibility.Public => 0,
              Accessibility.Protected => 1,
              Accessibility.Internal => 2,
              Accessibility.ProtectedOrInternal => 3,
              Accessibility.Private => 4,
              Accessibility.ProtectedAndInternal => 5,
              _ => -1
          };
      }
  }

  3.4 Reference Collector (Second Pass)

  public class ReferenceCollector : CSharpSyntaxWalker
  {
      private readonly SemanticModel _semanticModel;
      private readonly int _fileId;
      private readonly ICodeGraphRepository _repository;
      private readonly Stack<int> _contextStack = new();

      public int ReferenceCount { get; private set; }

      public ReferenceCollector(
          SemanticModel semanticModel,
          int fileId,
          ICodeGraphRepository repository)
      {
          _semanticModel = semanticModel;
          _fileId = fileId;
          _repository = repository;
      }

      public override void VisitInvocationExpression(InvocationExpressionSyntax node)
      {
          SymbolInfo symbolInfo = _semanticModel.GetSymbolInfo(node);

          if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
          {
              int contextId = GetCurrentContext();
              int targetId = _repository.GetOrCreateNodeId(
                  GetFullyQualifiedName(methodSymbol));

              int edgeId = _repository.RecordEdge(
                  contextId,
                  targetId,
                  CSharpReferenceKind.Call);

              RecordReferenceLocation(edgeId, node);
              ReferenceCount++;
          }

          base.VisitInvocationExpression(node);
      }

      public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
      {
          SymbolInfo symbolInfo = _semanticModel.GetSymbolInfo(node);

          if (symbolInfo.Symbol is IMethodSymbol constructor)
          {
              int contextId = GetCurrentContext();
              int targetId = _repository.GetOrCreateNodeId(
                  GetFullyQualifiedName(constructor.ContainingType));

              int edgeId = _repository.RecordEdge(
                  contextId,
                  targetId,
                  CSharpReferenceKind.Instantiation);

              RecordReferenceLocation(edgeId, node);
              ReferenceCount++;
          }

          base.VisitObjectCreationExpression(node);
      }

      public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
      {
          SymbolInfo symbolInfo = _semanticModel.GetSymbolInfo(node);
          ISymbol? symbol = symbolInfo.Symbol;

          if (symbol != null)
          {
              CSharpReferenceKind refKind = symbol switch
              {
                  IFieldSymbol => CSharpReferenceKind.FieldAccess,
                  IPropertySymbol => CSharpReferenceKind.PropertyAccess,
                  IEventSymbol => CSharpReferenceKind.EventAccess,
                  _ => CSharpReferenceKind.Unknown
              };

              if (refKind != CSharpReferenceKind.Unknown)
              {
                  int contextId = GetCurrentContext();
                  int targetId = _repository.GetOrCreateNodeId(
                      GetFullyQualifiedName(symbol));

                  int edgeId = _repository.RecordEdge(contextId, targetId, refKind);
                  RecordReferenceLocation(edgeId, node.Name);
                  ReferenceCount++;
              }
          }

          base.VisitMemberAccessExpression(node);
      }

      public override void VisitIdentifierName(IdentifierNameSyntax node)
      {
          // Track type references in declarations
          if (node.Parent is VariableDeclarationSyntax ||
              node.Parent is ParameterSyntax ||
              node.Parent is TypeOfExpressionSyntax)
          {
              TypeInfo typeInfo = _semanticModel.GetTypeInfo(node);

              if (typeInfo.Type is INamedTypeSymbol namedType)
              {
                  int contextId = GetCurrentContext();
                  int targetId = _repository.GetOrCreateNodeId(
                      GetFullyQualifiedName(namedType));

                  int edgeId = _repository.RecordEdge(
                      contextId,
                      targetId,
                      CSharpReferenceKind.TypeUsage);

                  RecordReferenceLocation(edgeId, node);
                  ReferenceCount++;
              }
          }

          base.VisitIdentifierName(node);
      }

      // Track context (which method/property we're currently in)
      public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
      {
          IMethodSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);
          if (symbol != null)
          {
              int nodeId = _repository.GetOrCreateNodeId(
                  GetFullyQualifiedName(symbol));
              _contextStack.Push(nodeId);
          }

          base.VisitMethodDeclaration(node);

          if (symbol != null) _contextStack.Pop();
      }

      private int GetCurrentContext()
      {
          return _contextStack.Count > 0 ? _contextStack.Peek() : 0;
      }

      private void RecordReferenceLocation(int edgeId, SyntaxNode node)
      {
          FileLinePositionSpan span = node.GetLocation().GetLineSpan();

          _repository.RecordOccurrence(
              edgeId,
              _fileId,
              span.StartLinePosition.Line + 1,
              span.StartLinePosition.Character + 1,
              span.EndLinePosition.Line + 1,
              span.EndLinePosition.Character + 1);
      }

      private string GetFullyQualifiedName(ISymbol symbol)
      {
          return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
      }
  }

  ---
  Part 4: Graph Visualization for the Webapp

  4.1 Technology Choices for Frontend Visualization

  Based on Sourcetrail's approach (Qt-based custom rendering) and modern web alternatives:

  | Option         | Pros                                      | Cons                                    |
  |----------------|-------------------------------------------|-----------------------------------------|
  | D3.js          | Maximum control, great for custom layouts | Steep learning curve, manual everything |
  | Cytoscape.js   | Built for graphs, good layout algorithms  | Can be heavy, learning curve            |
  | vis.js Network | Easy to use, good defaults                | Less customization                      |
  | React Flow     | Modern React component, good UX           | Requires React                          |
  | Sigma.js       | WebGL rendering, handles large graphs     | Different API paradigm                  |

  Recommendation: Cytoscape.js - best balance of power and ease of use for code graphs, supports:
  - Multiple layout algorithms (hierarchical, force-directed, dagre)
  - Compound nodes (for namespace/class containment)
  - Edge bundling
  - Pan/zoom
  - Node/edge styling
  - Event handling for click-to-navigate

  4.2 Frontend Architecture

  MyApp/wwwroot/
  ├── js/
  │   ├── code-graph/
  │   │   ├── graph-renderer.js      # Cytoscape wrapper
  │   │   ├── graph-layouts.js       # Layout configurations
  │   │   ├── graph-styles.js        # Node/edge styling
  │   │   ├── graph-interactions.js  # Click handlers, tooltips
  │   │   └── code-viewer.js         # Source code panel
  │   └── site.js                    # Existing file
  ├── css/
  │   └── code-graph.css             # Graph-specific styles
  └── lib/
      └── cytoscape/                 # Cytoscape.js library

  4.3 Graph Renderer Implementation

  // code-graph/graph-renderer.js

  class CodeGraphRenderer {
      constructor(containerId, options = {}) {
          this.container = document.getElementById(containerId);
          this.options = {
              minZoom: 0.1,
              maxZoom: 3,
              wheelSensitivity: 0.3,
              ...options
          };
          this.cy = null;
          this.currentLayout = 'dagre';
      }

      async initialize() {
          this.cy = cytoscape({
              container: this.container,
              style: this.getStyles(),
              minZoom: this.options.minZoom,
              maxZoom: this.options.maxZoom,
              wheelSensitivity: this.options.wheelSensitivity
          });

          this.setupEventHandlers();
          return this;
      }

      getStyles() {
          return [
              // Namespace nodes (compound/parent)
              {
                  selector: 'node[kind="namespace"]',
                  style: {
                      'shape': 'round-rectangle',
                      'background-color': '#1e293b',
                      'border-color': '#475569',
                      'border-width': 2,
                      'label': 'data(displayName)',
                      'font-size': '14px',
                      'color': '#94a3b8',
                      'text-valign': 'top',
                      'text-halign': 'center',
                      'padding': '20px'
                  }
              },
              // Class nodes
              {
                  selector: 'node[kind="class"]',
                  style: {
                      'shape': 'round-rectangle',
                      'background-color': '#3b82f6',
                      'border-color': '#60a5fa',
                      'border-width': 2,
                      'label': 'data(displayName)',
                      'font-size': '12px',
                      'color': '#fff',
                      'text-valign': 'center',
                      'text-halign': 'center',
                      'width': 'label',
                      'height': 'label',
                      'padding': '12px'
                  }
              },
              // Interface nodes
              {
                  selector: 'node[kind="interface"]',
                  style: {
                      'shape': 'diamond',
                      'background-color': '#8b5cf6',
                      'border-color': '#a78bfa',
                      'border-width': 2,
                      'label': 'data(displayName)',
                      'font-size': '12px',
                      'color': '#fff'
                  }
              },
              // Method nodes
              {
                  selector: 'node[kind="method"]',
                  style: {
                      'shape': 'ellipse',
                      'background-color': '#10b981',
                      'border-color': '#34d399',
                      'border-width': 1,
                      'label': 'data(displayName)',
                      'font-size': '10px',
                      'color': '#fff',
                      'width': 'label',
                      'height': 'label',
                      'padding': '8px'
                  }
              },
              // Inheritance edges
              {
                  selector: 'edge[type="inheritance"]',
                  style: {
                      'curve-style': 'bezier',
                      'target-arrow-shape': 'triangle-backcurve',
                      'target-arrow-color': '#f59e0b',
                      'line-color': '#f59e0b',
                      'width': 2,
                      'line-style': 'solid'
                  }
              },
              // Interface implementation edges
              {
                  selector: 'edge[type="interface"]',
                  style: {
                      'curve-style': 'bezier',
                      'target-arrow-shape': 'triangle-backcurve',
                      'target-arrow-color': '#8b5cf6',
                      'line-color': '#8b5cf6',
                      'width': 2,
                      'line-style': 'dashed'
                  }
              },
              // Call edges
              {
                  selector: 'edge[type="call"]',
                  style: {
                      'curve-style': 'bezier',
                      'target-arrow-shape': 'vee',
                      'target-arrow-color': '#64748b',
                      'line-color': '#64748b',
                      'width': 1
                  }
              },
              // Selected state
              {
                  selector: ':selected',
                  style: {
                      'border-width': 3,
                      'border-color': '#fbbf24',
                      'background-color': '#fef3c7'
                  }
              },
              // Hover state
              {
                  selector: 'node:active',
                  style: {
                      'overlay-opacity': 0.2,
                      'overlay-color': '#fff'
                  }
              }
          ];
      }

      setupEventHandlers() {
          // Click on node to show details
          this.cy.on('tap', 'node', (event) => {
              const node = event.target;
              this.onNodeSelected(node.data());
          });

          // Double-click to navigate to source
          this.cy.on('dbltap', 'node', (event) => {
              const node = event.target;
              this.navigateToSource(node.data());
          });

          // Right-click for context menu
          this.cy.on('cxttap', 'node', (event) => {
              const node = event.target;
              this.showContextMenu(event.originalEvent, node.data());
          });

          // Edge hover for reference info
          this.cy.on('mouseover', 'edge', (event) => {
              const edge = event.target;
              this.showEdgeTooltip(event.originalEvent, edge.data());
          });

          this.cy.on('mouseout', 'edge', () => {
              this.hideEdgeTooltip();
          });
      }

      async loadGraph(repositoryId, options = {}) {
          const params = new URLSearchParams({
              repositoryId: repositoryId,
              depth: options.depth || 2,
              includeMembers: options.includeMembers || false,
              rootSymbol: options.rootSymbol || ''
          });

          const response = await fetch(`/api/code-analysis/graph?${params}`);
          const graphData = await response.json();

          this.cy.elements().remove();
          this.cy.add(this.transformToElements(graphData));
          this.applyLayout(this.currentLayout);
      }

      transformToElements(graphData) {
          const elements = [];

          // Add nodes
          for (const node of graphData.nodes) {
              elements.push({
                  group: 'nodes',
                  data: {
                      id: node.id.toString(),
                      displayName: node.displayName,
                      kind: this.symbolKindToString(node.type),
                      fullyQualifiedName: node.serializedName,
                      parent: node.containerId ? node.containerId.toString() : null,
                      filePath: node.filePath,
                      line: node.line,
                      column: node.column
                  }
              });
          }

          // Add edges
          for (const edge of graphData.edges) {
              elements.push({
                  group: 'edges',
                  data: {
                      id: `e${edge.id}`,
                      source: edge.sourceNodeId.toString(),
                      target: edge.targetNodeId.toString(),
                      type: this.referenceKindToString(edge.type)
                  }
              });
          }

          return elements;
      }

      symbolKindToString(kind) {
          const kinds = {
              1: 'namespace', 10: 'class', 11: 'struct',
              12: 'interface', 13: 'enum', 14: 'delegate',
              15: 'record', 20: 'field', 21: 'property',
              22: 'method', 23: 'constructor'
          };
          return kinds[kind] || 'unknown';
      }

      referenceKindToString(kind) {
          const kinds = {
              1: 'inheritance', 2: 'interface', 10: 'call',
              11: 'typeUsage', 12: 'override', 30: 'contains'
          };
          return kinds[kind] || 'reference';
      }

      applyLayout(layoutName) {
          this.currentLayout = layoutName;

          const layouts = {
              dagre: {
                  name: 'dagre',
                  rankDir: 'TB',
                  nodeSep: 50,
                  rankSep: 100,
                  edgeSep: 10
              },
              cose: {
                  name: 'cose',
                  idealEdgeLength: 100,
                  nodeOverlap: 20,
                  nodeRepulsion: 400000,
                  animate: true
              },
              breadthfirst: {
                  name: 'breadthfirst',
                  directed: true,
                  spacingFactor: 1.5
              },
              circle: {
                  name: 'circle',
                  spacingFactor: 1.5
              }
          };

          const layout = this.cy.layout(layouts[layoutName] || layouts.dagre);
          layout.run();
      }

      // Callback methods for external handling
      onNodeSelected(nodeData) {
          document.dispatchEvent(new CustomEvent('codeGraph:nodeSelected', {
              detail: nodeData
          }));
      }

      navigateToSource(nodeData) {
          if (nodeData.filePath && nodeData.line) {
              document.dispatchEvent(new CustomEvent('codeGraph:navigateToSource', {
                  detail: {
                      filePath: nodeData.filePath,
                      line: nodeData.line,
                      column: nodeData.column
                  }
              }));
          }
      }

      showContextMenu(event, nodeData) {
          document.dispatchEvent(new CustomEvent('codeGraph:contextMenu', {
              detail: { event, nodeData }
          }));
      }

      // Public methods
      focusOnNode(nodeId) {
          const node = this.cy.$(`#${nodeId}`);
          if (node.length > 0) {
              this.cy.animate({
                  center: { eles: node },
                  zoom: 2,
                  duration: 300
              });
              node.select();
          }
      }

      highlightPath(sourceId, targetId) {
          const dijkstra = this.cy.elements().dijkstra(`#${sourceId}`);
          const path = dijkstra.pathTo(`#${targetId}`);

          this.cy.elements().removeClass('highlighted');
          path.addClass('highlighted');
      }

      expandNode(nodeId, depth = 1) {
          // Load more graph data centered on this node
          return this.loadGraph(null, {
              rootSymbol: this.cy.$(`#${nodeId}`).data('fullyQualifiedName'),
              depth: depth,
              includeMembers: true
          });
      }

      exportAsImage(format = 'png') {
          return this.cy[format]({
              output: 'blob',
              bg: '#0f172a',
              full: true,
              scale: 2
          });
      }

      destroy() {
          if (this.cy) {
              this.cy.destroy();
              this.cy = null;
          }
      }
  }

  // Export for use
  window.CodeGraphRenderer = CodeGraphRenderer;

  4.4 API Endpoints for Graph Data

  [ApiController]
  [Route("api/code-analysis")]
  public class CodeAnalysisController : ControllerBase
  {
      private readonly ICodeGraphRepository _repository;
      private readonly ICodeIndexer _indexer;

      [HttpPost("index")]
      public async Task<IActionResult> IndexRepository(
          [FromBody] IndexRepositoryRequest request,
          CancellationToken ct)
      {
          string solutionPath = Path.Combine(
              request.RepositoryPath,
              request.SolutionFile);

          IndexingResult result = await _indexer.IndexSolutionAsync(solutionPath, ct);

          return Ok(new
          {
              result.FilesIndexed,
              result.SymbolsCollected,
              result.ReferencesCollected,
              result.Errors
          });
      }

      [HttpGet("graph")]
      public async Task<IActionResult> GetGraph(
          [FromQuery] string repositoryId,
          [FromQuery] int depth = 2,
          [FromQuery] bool includeMembers = false,
          [FromQuery] string? rootSymbol = null,
          CancellationToken ct)
      {
          GraphQueryOptions options = new GraphQueryOptions
          {
              RepositoryId = repositoryId,
              Depth = Math.Min(depth, 5),
              IncludeMembers = includeMembers,
              RootSymbol = rootSymbol
          };

          GraphData graphData = await _repository.GetGraphDataAsync(options, ct);

          return Ok(graphData);
      }

      [HttpGet("symbols/{symbolId}/references")]
      public async Task<IActionResult> GetSymbolReferences(
          int symbolId,
          CancellationToken ct)
      {
          List<ReferenceLocation> references = await _repository
              .GetSymbolReferencesAsync(symbolId, ct);

          return Ok(references);
      }

      [HttpGet("symbols/{symbolId}/callers")]
      public async Task<IActionResult> GetCallers(int symbolId, CancellationToken ct)
      {
          List<CallerInfo> callers = await _repository
              .GetCallersAsync(symbolId, ct);

          return Ok(callers);
      }

      [HttpGet("symbols/{symbolId}/callees")]
      public async Task<IActionResult> GetCallees(int symbolId, CancellationToken ct)
      {
          List<CalleeInfo> callees = await _repository
              .GetCalleesAsync(symbolId, ct);

          return Ok(callees);
      }

      [HttpGet("inheritance/{symbolId}")]
      public async Task<IActionResult> GetInheritanceHierarchy(
          int symbolId,
          [FromQuery] bool ancestors = true,
          [FromQuery] bool descendants = true,
          CancellationToken ct)
      {
          InheritanceTree tree = await _repository
              .GetInheritanceTreeAsync(symbolId, ancestors, descendants, ct);

          return Ok(tree);
      }

      [HttpGet("search")]
      public async Task<IActionResult> SearchSymbols(
          [FromQuery] string query,
          [FromQuery] int limit = 20,
          CancellationToken ct)
      {
          List<SymbolSearchResult> results = await _repository
              .SearchSymbolsAsync(query, limit, ct);

          return Ok(results);
      }
  }

  ---
  Part 5: Integration with Existing Flow Webapp

  5.1 New Views for Code Analysis

  Views/
  ├── CodeAnalysis/
  │   ├── Index.cshtml              # Main code graph explorer
  │   ├── _GraphCanvas.cshtml       # Graph visualization partial
  │   ├── _SymbolDetails.cshtml     # Symbol info sidebar
  │   └── _SourceViewer.cshtml      # Code preview panel

  5.2 Main Code Analysis Page

  @* Views/CodeAnalysis/Index.cshtml *@
  @model MyApp.Models.CodeAnalysis.CodeAnalysisViewModel
  @{
      ViewData["Title"] = "Code Analysis";
  }

  <section class="flex h-[calc(100vh-4rem)] overflow-hidden text-gray-100">
      <!-- Left Sidebar: Symbol Tree -->
      <aside class="w-72 flex-shrink-0 overflow-y-auto border-r border-white/10 bg-gray-900/80">
          <div class="p-4">
              <h2 class="text-sm font-semibold text-white">Symbols</h2>
              <div class="mt-3">
                  <input type="text"
                         placeholder="Search symbols..."
                         class="w-full rounded-lg border border-white/10 bg-black/40 px-3 py-2 text-sm text-gray-100
  placeholder-gray-500 focus:border-indigo-400 focus:outline-none"
                         data-symbol-search="true" />
              </div>
              <nav class="mt-4 space-y-1" data-symbol-tree="true">
                  <!-- Symbol tree populated via JS -->
              </nav>
          </div>
      </aside>

      <!-- Center: Graph Canvas -->
      <main class="flex flex-1 flex-col">
          <!-- Toolbar -->
          <div class="flex items-center justify-between border-b border-white/10 bg-gray-900/60 px-4 py-2">
              <div class="flex items-center gap-3">
                  <select data-layout-selector="true"
                          class="rounded-md border border-white/10 bg-black/40 px-3 py-1.5 text-sm text-gray-200">
                      <option value="dagre">Hierarchical</option>
                      <option value="cose">Force-directed</option>
                      <option value="breadthfirst">Tree</option>
                      <option value="circle">Circular</option>
                  </select>
                  <button type="button" data-fit-graph="true"
                          class="rounded-md border border-white/10 bg-white/5 px-3 py-1.5 text-sm text-gray-200
  hover:bg-white/10">
                      Fit to view
                  </button>
                  <button type="button" data-export-graph="true"
                          class="rounded-md border border-white/10 bg-white/5 px-3 py-1.5 text-sm text-gray-200
  hover:bg-white/10">
                      Export PNG
                  </button>
              </div>
              <div class="flex items-center gap-2 text-xs text-gray-400">
                  <span data-node-count="true">0 nodes</span>
                  <span>|</span>
                  <span data-edge-count="true">0 edges</span>
              </div>
          </div>

          <!-- Graph Container -->
          <div id="code-graph-container"
               class="relative flex-1 bg-gray-950"
               data-repository-id="@Model.RepositoryId">
              <!-- Cytoscape renders here -->
          </div>
      </main>

      <!-- Right Sidebar: Details Panel -->
      <aside class="w-80 flex-shrink-0 overflow-y-auto border-l border-white/10 bg-gray-900/80">
          <div class="p-4" data-details-panel="true">
              <h2 class="text-sm font-semibold text-white">Symbol Details</h2>
              <p class="mt-2 text-sm text-gray-400">Click a node to view details</p>
          </div>

          <!-- Source Preview -->
          <div class="border-t border-white/10 p-4" data-source-preview="true">
              <h3 class="text-sm font-semibold text-white">Source</h3>
              <pre class="mt-3 max-h-64 overflow-auto rounded-lg bg-black/50 p-3 text-xs text-gray-300">
                  <code data-source-code="true"></code>
              </pre>
          </div>

          <!-- References List -->
          <div class="border-t border-white/10 p-4" data-references-panel="true">
              <h3 class="text-sm font-semibold text-white">References</h3>
              <ul class="mt-3 space-y-2" data-references-list="true">
                  <!-- Populated dynamically -->
              </ul>
          </div>
      </aside>
  </section>

  @section Scripts {
      <script src="~/lib/cytoscape/cytoscape.min.js"></script>
      <script src="~/lib/cytoscape/cytoscape-dagre.js"></script>
      <script src="~/js/code-graph/graph-renderer.js"></script>
      <script>
          document.addEventListener('DOMContentLoaded', async () => {
              const container = document.getElementById('code-graph-container');
              const repositoryId = container.dataset.repositoryId;

              const renderer = new CodeGraphRenderer('code-graph-container');
              await renderer.initialize();
              await renderer.loadGraph(repositoryId, { depth: 2 });

              // Layout selector
              document.querySelector('[data-layout-selector]')
                  .addEventListener('change', (e) => {
                      renderer.applyLayout(e.target.value);
                  });

              // Fit to view
              document.querySelector('[data-fit-graph]')
                  .addEventListener('click', () => {
                      renderer.cy.fit();
                  });

              // Export
              document.querySelector('[data-export-graph]')
                  .addEventListener('click', async () => {
                      const blob = await renderer.exportAsImage('png');
                      const url = URL.createObjectURL(blob);
                      const a = document.createElement('a');
                      a.href = url;
                      a.download = 'code-graph.png';
                      a.click();
                      URL.revokeObjectURL(url);
                  });

              // Node selection
              document.addEventListener('codeGraph:nodeSelected', (e) => {
                  updateDetailsPanel(e.detail);
                  loadReferences(e.detail.id);
              });

              // Navigate to source
              document.addEventListener('codeGraph:navigateToSource', (e) => {
                  loadSourcePreview(e.detail);
              });
          });

          async function updateDetailsPanel(nodeData) {
              const panel = document.querySelector('[data-details-panel]');
              panel.innerHTML = `
                  <h2 class="text-sm font-semibold text-white">${nodeData.displayName}</h2>
                  <dl class="mt-3 space-y-2 text-sm">
                      <div>
                          <dt class="text-gray-500">Kind</dt>
                          <dd class="text-gray-200">${nodeData.kind}</dd>
                      </div>
                      <div>
                          <dt class="text-gray-500">Full Name</dt>
                          <dd class="text-gray-200 break-all text-xs">${nodeData.fullyQualifiedName}</dd>
                      </div>
                      <div>
                          <dt class="text-gray-500">Location</dt>
                          <dd class="text-gray-200">${nodeData.filePath}:${nodeData.line}</dd>
                      </div>
                  </dl>
              `;
          }

          async function loadReferences(symbolId) {
              const response = await fetch(`/api/code-analysis/symbols/${symbolId}/references`);
              const references = await response.json();

              const list = document.querySelector('[data-references-list]');
              list.innerHTML = references.map(ref => `
                  <li class="rounded-md border border-white/10 bg-black/30 p-2 text-xs">
                      <span class="font-medium text-gray-200">${ref.filePath}</span>
                      <span class="text-gray-500">:${ref.line}</span>
                  </li>
              `).join('');
          }

          async function loadSourcePreview(location) {
              const params = new URLSearchParams({
                  filePath: location.filePath,
                  startLine: Math.max(1, location.line - 5),
                  endLine: location.line + 10
              });

              const response = await fetch(`/api/code-analysis/source?${params}`);
              const sourceCode = await response.text();

              const codeElement = document.querySelector('[data-source-code]');
              codeElement.textContent = sourceCode;
          }
      </script>
  }

  ---
  Part 6: Implementation Phases

  Phase 1: Foundation

  1. Create MyApp.CodeAnalysis project with domain models and enums
  2. Set up SQLite database schema using EF Core migrations
  3. Implement basic repository pattern for database access
  4. Add Roslyn NuGet packages: Microsoft.CodeAnalysis.CSharp.Workspaces, Microsoft.Build.Locator

  Phase 2: Core Indexer

  1. Implement MSBuildWorkspace solution/project loader
  2. Build SymbolDeclarationCollector syntax walker
  3. Build ReferenceCollector for second-pass analysis
  4. Create indexing orchestration with transaction support
  5. Add progress reporting for long-running indexing

  Phase 3: Query Layer

  1. Implement graph data queries (nodes + edges with filtering)
  2. Add symbol search functionality
  3. Create inheritance hierarchy queries
  4. Build call graph (callers/callees) queries
  5. Implement source location retrieval

  Phase 4: Frontend Visualization

  1. Add Cytoscape.js and dagre layout plugin
  2. Build CodeGraphRenderer class
  3. Create graph styling system matching your dark theme
  4. Implement node interactions (click, double-click, context menu)
  5. Add layout switching and export functionality

  Phase 5: Integration

  1. Create new views and controller routes
  2. Wire up repository selection to trigger indexing
  3. Connect graph visualization to API endpoints
  4. Add source code preview panel
  5. Implement navigation between graph and source

  Phase 6: Polish

  1. Add incremental/partial indexing
  2. Implement graph filtering (by namespace, type kind)
  3. Add path highlighting (shortest path between nodes)
  4. Performance optimization for large codebases
  5. Add graph persistence (remember zoom/pan state)

  ---
  Key Dependencies to Add

  <!-- MyApp.CodeAnalysis.csproj -->
  <ItemGroup>
    <!-- Roslyn -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.8.0" />
    <PackageReference Include="Microsoft.Build.Locator" Version="1.6.10" />

    <!-- For loading MSBuild projects -->
    <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.8.0" />

    <!-- Database -->
    <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
  </ItemGroup>

  <!-- Frontend (CDN or local) -->
  <script src="https://unpkg.com/cytoscape@3.28.1/dist/cytoscape.min.js"></script>
  <script src="https://unpkg.com/cytoscape-dagre@2.5.0/cytoscape-dagre.js"></script>
  <script src="https://unpkg.com/dagre@0.8.5/dist/dagre.min.js"></script>

  ---
  This implementation plan provides a complete roadmap for building a Roslyn-based code analysis system with graph visualization
  that integrates with your existing Flow webapp. The architecture follows clean architecture principles as specified in your
  CLAUDE.md, uses your existing tech stack (ASP.NET Core, SQLite, Tailwind CSS), and provides Sourcetrail-like functionality
  customized for C# development.