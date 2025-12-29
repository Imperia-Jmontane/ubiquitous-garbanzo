using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using MyApp.CodeAnalysis.Abstractions;

namespace MyApp.CodeAnalysis.Indexing
{
    public sealed class ReferenceCollector : CSharpSyntaxWalker
    {
        private readonly SemanticModel semanticModel;
        private readonly long fileId;
        private readonly long snapshotId;
        private readonly ISymbolCollectorRepository repository;
        private readonly Stack<long> contextStack;

        public ReferenceCollector(SemanticModel semanticModel, long fileId, long snapshotId, ISymbolCollectorRepository repository)
        {
            this.semanticModel = semanticModel;
            this.fileId = fileId;
            this.snapshotId = snapshotId;
            this.repository = repository;
            contextStack = new Stack<long>();
        }

        public int ReferenceCount { get; private set; }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            IMethodSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long contextId = repository.GetOrCreateNodeId(snapshotId, GetFullyQualifiedName(symbol));
                contextStack.Push(contextId);
            }

            base.VisitMethodDeclaration(node);

            if (symbol != null)
            {
                contextStack.Pop();
            }
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            IMethodSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long contextId = repository.GetOrCreateNodeId(snapshotId, GetFullyQualifiedName(symbol));
                contextStack.Push(contextId);
            }

            base.VisitConstructorDeclaration(node);

            if (symbol != null)
            {
                contextStack.Pop();
            }
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            IPropertySymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long contextId = repository.GetOrCreateNodeId(snapshotId, GetFullyQualifiedName(symbol));
                contextStack.Push(contextId);
            }

            base.VisitPropertyDeclaration(node);

            if (symbol != null)
            {
                contextStack.Pop();
            }
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                long? contextId = ResolveContextId(node);

                if (contextId.HasValue)
                {
                    long targetId = GetTargetNodeId(methodSymbol, GetSymbolKind(methodSymbol));
                    repository.RecordEdge(snapshotId, contextId.Value, targetId, CSharpReferenceKind.Call);
                    RecordReferenceLocation(targetId, node);
                    ReferenceCount++;
                }
            }

            base.VisitInvocationExpression(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol constructorSymbol)
            {
                long? contextId = ResolveContextId(node);

                if (contextId.HasValue)
                {
                    INamedTypeSymbol containingType = constructorSymbol.ContainingType;
                    long targetId = GetTargetNodeId(containingType, GetSymbolKind(containingType));
                    repository.RecordEdge(snapshotId, contextId.Value, targetId, CSharpReferenceKind.Instantiation);
                    RecordReferenceLocation(targetId, node);
                    ReferenceCount++;
                }
            }

            base.VisitObjectCreationExpression(node);
        }

        public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol constructorSymbol)
            {
                long? contextId = ResolveContextId(node);

                if (contextId.HasValue)
                {
                    INamedTypeSymbol containingType = constructorSymbol.ContainingType;
                    long targetId = GetTargetNodeId(containingType, GetSymbolKind(containingType));
                    repository.RecordEdge(snapshotId, contextId.Value, targetId, CSharpReferenceKind.Instantiation);
                    RecordReferenceLocation(targetId, node);
                    ReferenceCount++;
                }
            }

            base.VisitImplicitObjectCreationExpression(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node);
            ISymbol? symbol = symbolInfo.Symbol;

            if (symbol != null)
            {
                long? contextId = ResolveContextId(node);

                if (contextId.HasValue)
                {
                    CSharpReferenceKind referenceKind = GetReferenceKind(symbol);

                    if (referenceKind != CSharpReferenceKind.Unknown)
                    {
                        long targetId = GetTargetNodeId(symbol, GetSymbolKind(symbol));
                        repository.RecordEdge(snapshotId, contextId.Value, targetId, referenceKind);
                        RecordReferenceLocation(targetId, node.Name);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitMemberAccessExpression(node);
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            if (!node.Type.IsVar)
            {
                TypeInfo typeInfo = semanticModel.GetTypeInfo(node.Type);

                if (typeInfo.Type is INamedTypeSymbol namedType)
                {
                    long? contextId = ResolveContextId(node);

                    if (contextId.HasValue)
                    {
                        long targetId = GetTargetNodeId(namedType, GetSymbolKind(namedType));
                        repository.RecordEdge(snapshotId, contextId.Value, targetId, CSharpReferenceKind.TypeUsage);
                        RecordReferenceLocation(targetId, node.Type);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitVariableDeclaration(node);
        }

        public override void VisitParameter(ParameterSyntax node)
        {
            if (node.Type != null)
            {
                TypeInfo typeInfo = semanticModel.GetTypeInfo(node.Type);

                if (typeInfo.Type is INamedTypeSymbol namedType)
                {
                    long? contextId = ResolveContextId(node);

                    if (contextId.HasValue)
                    {
                        long targetId = GetTargetNodeId(namedType, GetSymbolKind(namedType));
                        repository.RecordEdge(snapshotId, contextId.Value, targetId, CSharpReferenceKind.TypeUsage);
                        RecordReferenceLocation(targetId, node.Type);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitParameter(node);
        }

        public override void VisitCastExpression(CastExpressionSyntax node)
        {
            TypeInfo typeInfo = semanticModel.GetTypeInfo(node.Type);

            if (typeInfo.Type is INamedTypeSymbol namedType)
            {
                long? contextId = ResolveContextId(node);

                if (contextId.HasValue)
                {
                    long targetId = GetTargetNodeId(namedType, GetSymbolKind(namedType));
                    repository.RecordEdge(snapshotId, contextId.Value, targetId, CSharpReferenceKind.Cast);
                    RecordReferenceLocation(targetId, node.Type);
                    ReferenceCount++;
                }
            }

            base.VisitCastExpression(node);
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            if (node.Expression != null)
            {
                TypeInfo typeInfo = semanticModel.GetTypeInfo(node.Expression);

                if (typeInfo.Type is INamedTypeSymbol exceptionType)
                {
                    long? contextId = ResolveContextId(node);

                    if (contextId.HasValue)
                    {
                        long targetId = GetTargetNodeId(exceptionType, GetSymbolKind(exceptionType));
                        repository.RecordEdge(snapshotId, contextId.Value, targetId, CSharpReferenceKind.Throw);
                        RecordReferenceLocation(targetId, node.Expression);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitThrowStatement(node);
        }

        public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
        {
            TypeInfo typeInfo = semanticModel.GetTypeInfo(node.Type);

            if (typeInfo.Type is INamedTypeSymbol exceptionType)
            {
                long? contextId = ResolveContextId(node);

                if (contextId.HasValue)
                {
                    long targetId = GetTargetNodeId(exceptionType, GetSymbolKind(exceptionType));
                    repository.RecordEdge(snapshotId, contextId.Value, targetId, CSharpReferenceKind.Catch);
                    RecordReferenceLocation(targetId, node.Type);
                    ReferenceCount++;
                }
            }

            base.VisitCatchDeclaration(node);
        }

        public override void VisitTypeArgumentList(TypeArgumentListSyntax node)
        {
            long? contextId = ResolveContextId(node);

            if (contextId.HasValue)
            {
                foreach (TypeSyntax typeArg in node.Arguments)
                {
                    TypeInfo typeInfo = semanticModel.GetTypeInfo(typeArg);

                    if (typeInfo.Type is INamedTypeSymbol namedType)
                    {
                        long targetId = GetTargetNodeId(namedType, GetSymbolKind(namedType));
                        repository.RecordEdge(snapshotId, contextId.Value, targetId, CSharpReferenceKind.TypeArgument);
                        RecordReferenceLocation(targetId, typeArg);
                        ReferenceCount++;
                    }
                }
            }

            base.VisitTypeArgumentList(node);
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol attributeConstructor)
            {
                long? contextId = ResolveContextId(node);

                if (!contextId.HasValue)
                {
                    contextId = ResolveContainingTypeContext(node);
                }

                if (contextId.HasValue)
                {
                    INamedTypeSymbol attributeType = attributeConstructor.ContainingType;
                    long targetId = GetTargetNodeId(attributeType, GetSymbolKind(attributeType));
                    repository.RecordEdge(snapshotId, contextId.Value, targetId, CSharpReferenceKind.AttributeUsage);
                    RecordReferenceLocation(targetId, node);
                    ReferenceCount++;
                }
            }

            base.VisitAttribute(node);
        }

        private long? ResolveContextId(SyntaxNode node)
        {
            if (contextStack.Count > 0)
            {
                return contextStack.Peek();
            }

            return ResolveContainingTypeContext(node);
        }

        private long? ResolveContainingTypeContext(SyntaxNode node)
        {
            BaseTypeDeclarationSyntax? typeDeclaration = node.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();

            if (typeDeclaration == null)
            {
                return null;
            }

            INamedTypeSymbol? typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);

            if (typeSymbol == null)
            {
                return null;
            }

            return repository.GetOrCreateNodeId(snapshotId, GetFullyQualifiedName(typeSymbol));
        }

        private void RecordReferenceLocation(long targetNodeId, SyntaxNode node)
        {
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();
            TextSpan textSpan = node.Span;

            repository.RecordSourceLocation(
                targetNodeId,
                fileId,
                span.StartLinePosition.Line + 1,
                span.StartLinePosition.Character + 1,
                span.EndLinePosition.Line + 1,
                span.EndLinePosition.Character + 1,
                textSpan.Start,
                textSpan.End,
                LocationType.Reference);
        }

        private long GetTargetNodeId(ISymbol symbol, CSharpSymbolKind kind)
        {
            string serializedName = GetFullyQualifiedName(symbol);
            string displayName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            if (IsExternalSymbol(symbol))
            {
                return repository.RecordExternalNode(snapshotId, serializedName, displayName, kind);
            }

            return repository.GetOrCreateNodeId(snapshotId, serializedName);
        }

        private bool IsExternalSymbol(ISymbol symbol)
        {
            return symbol.Locations.All(location => !location.IsInSource);
        }

        private CSharpReferenceKind GetReferenceKind(ISymbol symbol)
        {
            if (symbol is IFieldSymbol)
            {
                return CSharpReferenceKind.FieldAccess;
            }

            if (symbol is IPropertySymbol)
            {
                return CSharpReferenceKind.PropertyAccess;
            }

            if (symbol is IEventSymbol)
            {
                return CSharpReferenceKind.EventAccess;
            }

            return CSharpReferenceKind.Unknown;
        }

        private CSharpSymbolKind GetSymbolKind(ISymbol symbol)
        {
            if (symbol is INamespaceSymbol)
            {
                return CSharpSymbolKind.Namespace;
            }

            if (symbol is INamedTypeSymbol namedType)
            {
                return MapTypeKind(namedType.TypeKind, namedType.IsRecord);
            }

            if (symbol is IMethodSymbol methodSymbol)
            {
                return methodSymbol.MethodKind == MethodKind.Constructor
                    ? CSharpSymbolKind.Constructor
                    : CSharpSymbolKind.Method;
            }

            if (symbol is IPropertySymbol)
            {
                return CSharpSymbolKind.Property;
            }

            if (symbol is IFieldSymbol)
            {
                return CSharpSymbolKind.Field;
            }

            if (symbol is IEventSymbol)
            {
                return CSharpSymbolKind.Event;
            }

            if (symbol is IParameterSymbol)
            {
                return CSharpSymbolKind.Parameter;
            }

            if (symbol is ITypeParameterSymbol)
            {
                return CSharpSymbolKind.TypeParameter;
            }

            return CSharpSymbolKind.Unknown;
        }

        private CSharpSymbolKind MapTypeKind(TypeKind typeKind, bool isRecord)
        {
            if (isRecord)
            {
                return typeKind == TypeKind.Struct ? CSharpSymbolKind.RecordStruct : CSharpSymbolKind.Record;
            }

            return typeKind switch
            {
                TypeKind.Class => CSharpSymbolKind.Class,
                TypeKind.Struct => CSharpSymbolKind.Struct,
                TypeKind.Interface => CSharpSymbolKind.Interface,
                TypeKind.Enum => CSharpSymbolKind.Enum,
                TypeKind.Delegate => CSharpSymbolKind.Delegate,
                _ => CSharpSymbolKind.Unknown
            };
        }

        private string GetFullyQualifiedName(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
    }
}

