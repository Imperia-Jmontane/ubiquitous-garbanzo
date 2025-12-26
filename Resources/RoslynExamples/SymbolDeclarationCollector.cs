// ============================================================================
// REFERENCE FILE: Symbol Declaration Collector using Roslyn
// This is a reference implementation for the junior developer.
// DO NOT use directly - adapt to your project structure.
// ============================================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace MyApp.CodeAnalysis.Reference
{
    /// <summary>
    /// Example CSharpSyntaxWalker that collects symbol declarations from C# source code.
    ///
    /// HOW IT WORKS:
    /// 1. The walker visits each node in the syntax tree
    /// 2. For each declaration (class, method, property, etc.), it extracts:
    ///    - The symbol information from the semantic model
    ///    - Source location (file, line, column)
    ///    - Relationships (inheritance, containment)
    /// 3. Data is stored in the repository for later graph visualization
    ///
    /// KEY ROSLYN CONCEPTS:
    /// - SyntaxNode: Represents code structure (what you see in the editor)
    /// - SemanticModel: Provides meaning (what types are, what references resolve to)
    /// - ISymbol: Represents a named entity (class, method, variable, etc.)
    /// </summary>
    public class SymbolDeclarationCollector : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly int _fileId;
        private readonly ICodeGraphRepository _repository;

        // Stack to track the current container (namespace -> class -> method)
        // This allows us to record containment relationships
        private readonly Stack<int> _containerStack = new Stack<int>();

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

        // ====================================================================
        // NAMESPACE DECLARATIONS
        // Example: namespace MyApp.Services { ... }
        // ====================================================================
        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            // GetDeclaredSymbol gives us the INamespaceSymbol for this declaration
            INamespaceSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Namespace);
                RecordContainment(nodeId);
                _containerStack.Push(nodeId);
            }

            // IMPORTANT: Call base to continue walking child nodes
            base.VisitNamespaceDeclaration(node);

            if (symbol != null)
            {
                _containerStack.Pop();
            }
        }

        // Also handle file-scoped namespaces (C# 10+)
        // Example: namespace MyApp.Services;
        public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
        {
            INamespaceSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Namespace);
                RecordContainment(nodeId);
                _containerStack.Push(nodeId);
            }

            base.VisitFileScopedNamespaceDeclaration(node);

            if (symbol != null)
            {
                _containerStack.Pop();
            }
        }

        // ====================================================================
        // CLASS DECLARATIONS
        // Example: public class MyService { ... }
        // ====================================================================
        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            INamedTypeSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                // Determine if it's a regular class or a record class
                CSharpSymbolKind kind = node.Keyword.ValueText switch
                {
                    "record" => CSharpSymbolKind.Record,
                    _ => CSharpSymbolKind.Class
                };

                int nodeId = RecordSymbol(symbol, node, kind);
                RecordContainment(nodeId);

                // Record inheritance and interface implementations
                RecordTypeRelationships(symbol, nodeId);

                _containerStack.Push(nodeId);
            }

            base.VisitClassDeclaration(node);

            if (symbol != null)
            {
                _containerStack.Pop();
            }
        }

        // ====================================================================
        // INTERFACE DECLARATIONS
        // Example: public interface IMyService { ... }
        // ====================================================================
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

            if (symbol != null)
            {
                _containerStack.Pop();
            }
        }

        // ====================================================================
        // STRUCT DECLARATIONS
        // Example: public struct MyStruct { ... }
        // ====================================================================
        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            INamedTypeSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                CSharpSymbolKind kind = node.Keyword.ValueText switch
                {
                    "record" => CSharpSymbolKind.RecordStruct,
                    _ => CSharpSymbolKind.Struct
                };

                int nodeId = RecordSymbol(symbol, node, kind);
                RecordContainment(nodeId);
                RecordTypeRelationships(symbol, nodeId);
                _containerStack.Push(nodeId);
            }

            base.VisitStructDeclaration(node);

            if (symbol != null)
            {
                _containerStack.Pop();
            }
        }

        // ====================================================================
        // ENUM DECLARATIONS
        // Example: public enum Status { Active, Inactive }
        // ====================================================================
        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            INamedTypeSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Enum);
                RecordContainment(nodeId);
                _containerStack.Push(nodeId);
            }

            base.VisitEnumDeclaration(node);

            if (symbol != null)
            {
                _containerStack.Pop();
            }
        }

        // ====================================================================
        // METHOD DECLARATIONS
        // Example: public async Task<string> GetDataAsync() { ... }
        // ====================================================================
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            IMethodSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Method);
                RecordContainment(nodeId);

                // If this method overrides a base method, record that relationship
                if (symbol.OverriddenMethod != null)
                {
                    string baseName = GetFullyQualifiedName(symbol.OverriddenMethod);
                    int targetId = _repository.GetOrCreateNodeId(baseName);
                    _repository.RecordEdge(nodeId, targetId, CSharpReferenceKind.Override);
                }

                // If this method implements an interface method, record that too
                foreach (IMethodSymbol interfaceMethod in symbol.ExplicitInterfaceImplementations)
                {
                    string interfaceName = GetFullyQualifiedName(interfaceMethod);
                    int targetId = _repository.GetOrCreateNodeId(interfaceName);
                    _repository.RecordEdge(nodeId, targetId, CSharpReferenceKind.InterfaceImplementation);
                }
            }

            base.VisitMethodDeclaration(node);
        }

        // ====================================================================
        // CONSTRUCTOR DECLARATIONS
        // Example: public MyClass(string name) { ... }
        // ====================================================================
        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            IMethodSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Constructor);
                RecordContainment(nodeId);
            }

            base.VisitConstructorDeclaration(node);
        }

        // ====================================================================
        // PROPERTY DECLARATIONS
        // Example: public string Name { get; set; }
        // ====================================================================
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            IPropertySymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Property);
                RecordContainment(nodeId);

                // Record the type being used
                if (symbol.Type is INamedTypeSymbol propertyType)
                {
                    string typeName = GetFullyQualifiedName(propertyType);
                    int typeId = _repository.GetOrCreateNodeId(typeName);
                    _repository.RecordEdge(nodeId, typeId, CSharpReferenceKind.TypeUsage);
                }
            }

            base.VisitPropertyDeclaration(node);
        }

        // ====================================================================
        // FIELD DECLARATIONS
        // Example: private readonly ILogger _logger;
        // ====================================================================
        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            // A field declaration can declare multiple fields:
            // private int x, y, z;
            foreach (VariableDeclaratorSyntax variable in node.Declaration.Variables)
            {
                IFieldSymbol? symbol = _semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;

                if (symbol != null)
                {
                    int nodeId = RecordSymbol(symbol, variable, CSharpSymbolKind.Field);
                    RecordContainment(nodeId);

                    // Record the type being used
                    if (symbol.Type is INamedTypeSymbol fieldType)
                    {
                        string typeName = GetFullyQualifiedName(fieldType);
                        int typeId = _repository.GetOrCreateNodeId(typeName);
                        _repository.RecordEdge(nodeId, typeId, CSharpReferenceKind.TypeUsage);
                    }
                }
            }

            base.VisitFieldDeclaration(node);
        }

        // ====================================================================
        // EVENT DECLARATIONS
        // Example: public event EventHandler OnDataChanged;
        // ====================================================================
        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            IEventSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Event);
                RecordContainment(nodeId);
            }

            base.VisitEventDeclaration(node);
        }

        // ====================================================================
        // DELEGATE DECLARATIONS
        // Example: public delegate void MyCallback(string message);
        // ====================================================================
        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            INamedTypeSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Delegate);
                RecordContainment(nodeId);
            }

            base.VisitDelegateDeclaration(node);
        }

        // ====================================================================
        // ENUM MEMBER DECLARATIONS
        // Example: Active = 1,
        // ====================================================================
        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            IFieldSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                int nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.EnumMember);
                RecordContainment(nodeId);
            }

            base.VisitEnumMemberDeclaration(node);
        }

        // ====================================================================
        // HELPER METHODS
        // ====================================================================

        /// <summary>
        /// Records a symbol in the database with its source location.
        /// </summary>
        private int RecordSymbol(ISymbol symbol, SyntaxNode node, CSharpSymbolKind kind)
        {
            // Get the fully qualified name for unique identification
            string fullyQualifiedName = GetFullyQualifiedName(symbol);

            // Get a human-readable display name
            string displayName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            // Get the source location
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();

            // Record the node in the database
            int nodeId = _repository.RecordNode(
                fullyQualifiedName,
                displayName,
                kind,
                GetAccessibility(symbol),
                symbol.IsStatic,
                symbol.IsAbstract,
                symbol.IsVirtual,
                symbol.IsOverride);

            // Record the source location (line numbers are 0-based, we convert to 1-based)
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

        /// <summary>
        /// Records the containment relationship (e.g., class contains method).
        /// </summary>
        private void RecordContainment(int childId)
        {
            if (_containerStack.Count > 0)
            {
                int containerId = _containerStack.Peek();
                _repository.RecordEdge(containerId, childId, CSharpReferenceKind.Contains);
            }
        }

        /// <summary>
        /// Records inheritance and interface implementation relationships.
        /// </summary>
        private void RecordTypeRelationships(INamedTypeSymbol typeSymbol, int nodeId)
        {
            // Record base class (skip System.Object as it's implicit)
            if (typeSymbol.BaseType != null &&
                typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
            {
                string baseName = GetFullyQualifiedName(typeSymbol.BaseType);
                int baseId = _repository.GetOrCreateNodeId(baseName);
                _repository.RecordEdge(nodeId, baseId, CSharpReferenceKind.Inheritance);
            }

            // Record implemented interfaces
            foreach (INamedTypeSymbol interfaceSymbol in typeSymbol.Interfaces)
            {
                string interfaceName = GetFullyQualifiedName(interfaceSymbol);
                int interfaceId = _repository.GetOrCreateNodeId(interfaceName);
                _repository.RecordEdge(nodeId, interfaceId, CSharpReferenceKind.InterfaceImplementation);
            }
        }

        /// <summary>
        /// Gets the fully qualified name for a symbol (used as unique identifier).
        /// </summary>
        private string GetFullyQualifiedName(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        /// <summary>
        /// Converts accessibility to an integer for storage.
        /// </summary>
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

    // ========================================================================
    // PLACEHOLDER INTERFACES AND ENUMS
    // These would be defined in your actual project
    // ========================================================================

    public interface ICodeGraphRepository
    {
        int RecordNode(string fullyQualifiedName, string displayName, CSharpSymbolKind kind,
            int accessibility, bool isStatic, bool isAbstract, bool isVirtual, bool isOverride);
        void RecordSourceLocation(int nodeId, int fileId, int startLine, int startColumn,
            int endLine, int endColumn, LocationType locationType);
        void RecordEdge(int sourceId, int targetId, CSharpReferenceKind referenceKind);
        int GetOrCreateNodeId(string fullyQualifiedName);
    }

    public enum CSharpSymbolKind
    {
        Unknown = 0,
        Namespace = 1,
        Class = 10,
        Struct = 11,
        Interface = 12,
        Enum = 13,
        Delegate = 14,
        Record = 15,
        RecordStruct = 16,
        Field = 20,
        Property = 21,
        Method = 22,
        Constructor = 23,
        Event = 27,
        EnumMember = 28
    }

    public enum CSharpReferenceKind
    {
        Unknown = 0,
        Inheritance = 1,
        InterfaceImplementation = 2,
        Call = 10,
        TypeUsage = 11,
        Override = 12,
        Contains = 30
    }

    public enum LocationType
    {
        Definition = 0,
        Reference = 1,
        Scope = 2
    }
}
