// ============================================================================
// REFERENCE FILE: Reference Collector using Roslyn
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
    /// Example CSharpSyntaxWalker that collects references (calls, usages) from C# source code.
    ///
    /// HOW IT WORKS:
    /// This is the SECOND PASS of indexing. After SymbolDeclarationCollector has recorded
    /// all symbol declarations, this collector finds all the places where those symbols
    /// are USED (method calls, type references, property accesses, etc.)
    ///
    /// WHY TWO PASSES?
    /// 1. First pass (SymbolDeclarationCollector): Records all definitions
    /// 2. Second pass (ReferenceCollector): Records all usages
    ///
    /// This ensures that when we record "MethodA calls MethodB", both MethodA and MethodB
    /// already exist in the database with valid IDs.
    ///
    /// KEY ROSLYN CONCEPTS:
    /// - GetSymbolInfo(): Resolves what a name/expression refers to
    /// - GetTypeInfo(): Gets the type of an expression
    /// - SymbolInfo.Symbol: The resolved symbol (null if unresolved)
    /// </summary>
    public class ReferenceCollector : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly int _fileId;
        private readonly ICodeGraphRepository _repository;

        // Stack to track the current context (which method/property we're inside)
        // This tells us WHO is making the call/reference
        private readonly Stack<int> _contextStack = new Stack<int>();

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

        // ====================================================================
        // CONTEXT TRACKING
        // We need to know "who" is making the call. These methods track that.
        // ====================================================================

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            IMethodSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                // Get or create the node ID for this method
                string name = GetFullyQualifiedName(symbol);
                int nodeId = _repository.GetOrCreateNodeId(name);
                _contextStack.Push(nodeId);
            }

            base.VisitMethodDeclaration(node);

            if (symbol != null)
            {
                _contextStack.Pop();
            }
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            IMethodSymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                string name = GetFullyQualifiedName(symbol);
                int nodeId = _repository.GetOrCreateNodeId(name);
                _contextStack.Push(nodeId);
            }

            base.VisitConstructorDeclaration(node);

            if (symbol != null)
            {
                _contextStack.Pop();
            }
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            IPropertySymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                string name = GetFullyQualifiedName(symbol);
                int nodeId = _repository.GetOrCreateNodeId(name);
                _contextStack.Push(nodeId);
            }

            base.VisitPropertyDeclaration(node);

            if (symbol != null)
            {
                _contextStack.Pop();
            }
        }

        // ====================================================================
        // METHOD INVOCATIONS (CALLS)
        // Example: myService.GetDataAsync()
        // ====================================================================
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // GetSymbolInfo resolves what method is being called
            SymbolInfo symbolInfo = _semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                int contextId = GetCurrentContext();

                // Skip if we're not inside a method (e.g., field initializer)
                if (contextId > 0)
                {
                    string targetName = GetFullyQualifiedName(methodSymbol);
                    int targetId = _repository.GetOrCreateNodeId(targetName);

                    // Record: "contextId CALLS targetId"
                    int edgeId = _repository.RecordEdge(
                        contextId,
                        targetId,
                        CSharpReferenceKind.Call);

                    // Record where this call happens in the source code
                    RecordReferenceLocation(edgeId, node);
                    ReferenceCount++;
                }
            }

            base.VisitInvocationExpression(node);
        }

        // ====================================================================
        // OBJECT CREATION (new T())
        // Example: var service = new MyService();
        // ====================================================================
        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            SymbolInfo symbolInfo = _semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol constructor)
            {
                int contextId = GetCurrentContext();

                if (contextId > 0)
                {
                    // Record instantiation of the containing type
                    string typeName = GetFullyQualifiedName(constructor.ContainingType);
                    int typeId = _repository.GetOrCreateNodeId(typeName);

                    int edgeId = _repository.RecordEdge(
                        contextId,
                        typeId,
                        CSharpReferenceKind.Instantiation);

                    RecordReferenceLocation(edgeId, node);
                    ReferenceCount++;
                }
            }

            base.VisitObjectCreationExpression(node);
        }

        // ====================================================================
        // IMPLICIT OBJECT CREATION (new())
        // Example: MyService service = new();
        // ====================================================================
        public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
        {
            SymbolInfo symbolInfo = _semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol constructor)
            {
                int contextId = GetCurrentContext();

                if (contextId > 0)
                {
                    string typeName = GetFullyQualifiedName(constructor.ContainingType);
                    int typeId = _repository.GetOrCreateNodeId(typeName);

                    int edgeId = _repository.RecordEdge(
                        contextId,
                        typeId,
                        CSharpReferenceKind.Instantiation);

                    RecordReferenceLocation(edgeId, node);
                    ReferenceCount++;
                }
            }

            base.VisitImplicitObjectCreationExpression(node);
        }

        // ====================================================================
        // MEMBER ACCESS (field, property, event access)
        // Example: myObject.PropertyName
        // ====================================================================
        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            SymbolInfo symbolInfo = _semanticModel.GetSymbolInfo(node);
            ISymbol? symbol = symbolInfo.Symbol;

            if (symbol != null)
            {
                int contextId = GetCurrentContext();

                if (contextId > 0)
                {
                    // Determine the reference kind based on symbol type
                    CSharpReferenceKind refKind = symbol switch
                    {
                        IFieldSymbol => CSharpReferenceKind.FieldAccess,
                        IPropertySymbol => CSharpReferenceKind.PropertyAccess,
                        IEventSymbol => CSharpReferenceKind.EventAccess,
                        _ => CSharpReferenceKind.Unknown
                    };

                    if (refKind != CSharpReferenceKind.Unknown)
                    {
                        string targetName = GetFullyQualifiedName(symbol);
                        int targetId = _repository.GetOrCreateNodeId(targetName);

                        int edgeId = _repository.RecordEdge(contextId, targetId, refKind);
                        RecordReferenceLocation(edgeId, node.Name);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitMemberAccessExpression(node);
        }

        // ====================================================================
        // TYPE REFERENCES IN VARIABLE DECLARATIONS
        // Example: MyService service = ...
        //          ^^^^^^^^^ This type reference
        // ====================================================================
        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            // Skip 'var' declarations - we handle those via type inference
            if (!node.Type.IsVar)
            {
                TypeInfo typeInfo = _semanticModel.GetTypeInfo(node.Type);

                if (typeInfo.Type is INamedTypeSymbol namedType)
                {
                    int contextId = GetCurrentContext();

                    if (contextId > 0)
                    {
                        string typeName = GetFullyQualifiedName(namedType);
                        int typeId = _repository.GetOrCreateNodeId(typeName);

                        int edgeId = _repository.RecordEdge(
                            contextId,
                            typeId,
                            CSharpReferenceKind.TypeUsage);

                        RecordReferenceLocation(edgeId, node.Type);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitVariableDeclaration(node);
        }

        // ====================================================================
        // PARAMETER TYPES
        // Example: public void Process(MyService service) { }
        //                              ^^^^^^^^^ This type reference
        // ====================================================================
        public override void VisitParameter(ParameterSyntax node)
        {
            if (node.Type != null)
            {
                TypeInfo typeInfo = _semanticModel.GetTypeInfo(node.Type);

                if (typeInfo.Type is INamedTypeSymbol namedType)
                {
                    int contextId = GetCurrentContext();

                    if (contextId > 0)
                    {
                        string typeName = GetFullyQualifiedName(namedType);
                        int typeId = _repository.GetOrCreateNodeId(typeName);

                        int edgeId = _repository.RecordEdge(
                            contextId,
                            typeId,
                            CSharpReferenceKind.TypeUsage);

                        RecordReferenceLocation(edgeId, node.Type);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitParameter(node);
        }

        // ====================================================================
        // CAST EXPRESSIONS
        // Example: var result = (MyType)obj;
        // ====================================================================
        public override void VisitCastExpression(CastExpressionSyntax node)
        {
            TypeInfo typeInfo = _semanticModel.GetTypeInfo(node.Type);

            if (typeInfo.Type is INamedTypeSymbol namedType)
            {
                int contextId = GetCurrentContext();

                if (contextId > 0)
                {
                    string typeName = GetFullyQualifiedName(namedType);
                    int typeId = _repository.GetOrCreateNodeId(typeName);

                    int edgeId = _repository.RecordEdge(
                        contextId,
                        typeId,
                        CSharpReferenceKind.Cast);

                    RecordReferenceLocation(edgeId, node.Type);
                    ReferenceCount++;
                }
            }

            base.VisitCastExpression(node);
        }

        // ====================================================================
        // AS EXPRESSIONS
        // Example: var result = obj as MyType;
        // ====================================================================
        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.AsExpression) || node.IsKind(SyntaxKind.IsExpression))
            {
                TypeInfo typeInfo = _semanticModel.GetTypeInfo(node.Right);

                if (typeInfo.Type is INamedTypeSymbol namedType)
                {
                    int contextId = GetCurrentContext();

                    if (contextId > 0)
                    {
                        string typeName = GetFullyQualifiedName(namedType);
                        int typeId = _repository.GetOrCreateNodeId(typeName);

                        int edgeId = _repository.RecordEdge(
                            contextId,
                            typeId,
                            CSharpReferenceKind.Cast);

                        RecordReferenceLocation(edgeId, node.Right);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitBinaryExpression(node);
        }

        // ====================================================================
        // THROW EXPRESSIONS
        // Example: throw new ArgumentException();
        // ====================================================================
        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            if (node.Expression != null)
            {
                TypeInfo typeInfo = _semanticModel.GetTypeInfo(node.Expression);

                if (typeInfo.Type is INamedTypeSymbol exceptionType)
                {
                    int contextId = GetCurrentContext();

                    if (contextId > 0)
                    {
                        string typeName = GetFullyQualifiedName(exceptionType);
                        int typeId = _repository.GetOrCreateNodeId(typeName);

                        int edgeId = _repository.RecordEdge(
                            contextId,
                            typeId,
                            CSharpReferenceKind.Throw);

                        RecordReferenceLocation(edgeId, node.Expression);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitThrowStatement(node);
        }

        // ====================================================================
        // CATCH CLAUSES
        // Example: catch (ArgumentException ex) { }
        // ====================================================================
        public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
        {
            TypeInfo typeInfo = _semanticModel.GetTypeInfo(node.Type);

            if (typeInfo.Type is INamedTypeSymbol exceptionType)
            {
                int contextId = GetCurrentContext();

                if (contextId > 0)
                {
                    string typeName = GetFullyQualifiedName(exceptionType);
                    int typeId = _repository.GetOrCreateNodeId(typeName);

                    int edgeId = _repository.RecordEdge(
                        contextId,
                        typeId,
                        CSharpReferenceKind.Catch);

                    RecordReferenceLocation(edgeId, node.Type);
                    ReferenceCount++;
                }
            }

            base.VisitCatchDeclaration(node);
        }

        // ====================================================================
        // GENERIC TYPE ARGUMENTS
        // Example: List<MyType>
        //               ^^^^^^ This type argument
        // ====================================================================
        public override void VisitTypeArgumentList(TypeArgumentListSyntax node)
        {
            int contextId = GetCurrentContext();

            if (contextId > 0)
            {
                foreach (TypeSyntax typeArg in node.Arguments)
                {
                    TypeInfo typeInfo = _semanticModel.GetTypeInfo(typeArg);

                    if (typeInfo.Type is INamedTypeSymbol namedType)
                    {
                        string typeName = GetFullyQualifiedName(namedType);
                        int typeId = _repository.GetOrCreateNodeId(typeName);

                        int edgeId = _repository.RecordEdge(
                            contextId,
                            typeId,
                            CSharpReferenceKind.TypeArgument);

                        RecordReferenceLocation(edgeId, typeArg);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitTypeArgumentList(node);
        }

        // ====================================================================
        // ATTRIBUTE USAGES
        // Example: [HttpGet("api/data")]
        // ====================================================================
        public override void VisitAttribute(AttributeSyntax node)
        {
            SymbolInfo symbolInfo = _semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol attributeConstructor)
            {
                int contextId = GetCurrentContext();

                // Attributes can be on types too, not just methods
                // If contextId is 0, try to get the containing type
                if (contextId == 0)
                {
                    // Find the nearest containing type declaration
                    SyntaxNode? parent = node.Parent?.Parent;
                    while (parent != null)
                    {
                        if (parent is TypeDeclarationSyntax typeDecl)
                        {
                            INamedTypeSymbol? typeSymbol = _semanticModel.GetDeclaredSymbol(typeDecl);
                            if (typeSymbol != null)
                            {
                                contextId = _repository.GetOrCreateNodeId(GetFullyQualifiedName(typeSymbol));
                                break;
                            }
                        }
                        parent = parent.Parent;
                    }
                }

                if (contextId > 0)
                {
                    string attrTypeName = GetFullyQualifiedName(attributeConstructor.ContainingType);
                    int attrTypeId = _repository.GetOrCreateNodeId(attrTypeName);

                    int edgeId = _repository.RecordEdge(
                        contextId,
                        attrTypeId,
                        CSharpReferenceKind.AttributeUsage);

                    RecordReferenceLocation(edgeId, node);
                    ReferenceCount++;
                }
            }

            base.VisitAttribute(node);
        }

        // ====================================================================
        // HELPER METHODS
        // ====================================================================

        /// <summary>
        /// Gets the current context (the method/property we're inside).
        /// Returns 0 if not inside any tracked context.
        /// </summary>
        private int GetCurrentContext()
        {
            return _contextStack.Count > 0 ? _contextStack.Peek() : 0;
        }

        /// <summary>
        /// Records the source location of a reference.
        /// </summary>
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

        /// <summary>
        /// Gets the fully qualified name for a symbol.
        /// </summary>
        private string GetFullyQualifiedName(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
    }

    // ========================================================================
    // EXTENDED ENUMS (adding to the ones from SymbolDeclarationCollector)
    // ========================================================================

    // Add these to CSharpReferenceKind enum:
    // FieldAccess = 20,
    // PropertyAccess = 21,
    // EventAccess = 22,
    // TypeArgument = 41,
    // AttributeUsage = 42,
    // Instantiation = 50,
    // Cast = 51,
    // Throw = 52,
    // Catch = 53
}
