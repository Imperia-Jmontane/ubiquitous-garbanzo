using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using MyApp.CodeAnalysis.Abstractions;

namespace MyApp.CodeAnalysis.Indexing
{
    public sealed class SymbolDeclarationCollector : CSharpSyntaxWalker
    {
        private readonly SemanticModel semanticModel;
        private readonly long fileId;
        private readonly long snapshotId;
        private readonly ISymbolCollectorRepository repository;
        private readonly Stack<long> containerStack;

        public SymbolDeclarationCollector(SemanticModel semanticModel, long fileId, long snapshotId, ISymbolCollectorRepository repository)
        {
            this.semanticModel = semanticModel;
            this.fileId = fileId;
            this.snapshotId = snapshotId;
            this.repository = repository;
            containerStack = new Stack<long>();
        }

        public int SymbolCount { get; private set; }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            INamespaceSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Namespace, null, false, false, false, false, false, false);
                RecordContainment(nodeId);
                containerStack.Push(nodeId);
            }

            base.VisitNamespaceDeclaration(node);

            if (symbol != null)
            {
                containerStack.Pop();
            }
        }

        public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
        {
            INamespaceSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Namespace, null, false, false, false, false, false, false);
                RecordContainment(nodeId);
                containerStack.Push(nodeId);
            }

            base.VisitFileScopedNamespaceDeclaration(node);

            if (symbol != null)
            {
                containerStack.Pop();
            }
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            INamedTypeSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Class, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                RecordContainment(nodeId);
                RecordTypeRelationships(symbol, nodeId);
                containerStack.Push(nodeId);
            }

            base.VisitClassDeclaration(node);

            if (symbol != null)
            {
                containerStack.Pop();
            }
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            INamedTypeSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Interface, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                RecordContainment(nodeId);
                RecordTypeRelationships(symbol, nodeId);
                containerStack.Push(nodeId);
            }

            base.VisitInterfaceDeclaration(node);

            if (symbol != null)
            {
                containerStack.Pop();
            }
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            INamedTypeSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Struct, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                RecordContainment(nodeId);
                RecordTypeRelationships(symbol, nodeId);
                containerStack.Push(nodeId);
            }

            base.VisitStructDeclaration(node);

            if (symbol != null)
            {
                containerStack.Pop();
            }
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            INamedTypeSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                CSharpSymbolKind kind = node.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? CSharpSymbolKind.RecordStruct : CSharpSymbolKind.Record;
                long nodeId = RecordSymbol(symbol, node, kind, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                RecordContainment(nodeId);
                RecordTypeRelationships(symbol, nodeId);
                containerStack.Push(nodeId);
            }

            base.VisitRecordDeclaration(node);

            if (symbol != null)
            {
                containerStack.Pop();
            }
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            INamedTypeSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Enum, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                RecordContainment(nodeId);
                containerStack.Push(nodeId);
            }

            base.VisitEnumDeclaration(node);

            if (symbol != null)
            {
                containerStack.Pop();
            }
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            IMethodSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Method, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, symbol.IsExtensionMethod, symbol.IsAsync);
                RecordContainment(nodeId);
                RecordMethodRelationships(symbol, nodeId);
            }

            base.VisitMethodDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            IMethodSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Constructor, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                RecordContainment(nodeId);
            }

            base.VisitConstructorDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            IPropertySymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Property, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                RecordContainment(nodeId);
                RecordTypeUsage(symbol.Type, nodeId);
            }

            base.VisitPropertyDeclaration(node);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            foreach (VariableDeclaratorSyntax variable in node.Declaration.Variables)
            {
                IFieldSymbol? symbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;

                if (symbol != null)
                {
                    long nodeId = RecordSymbol(symbol, variable, CSharpSymbolKind.Field, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                    RecordContainment(nodeId);
                    RecordTypeUsage(symbol.Type, nodeId);
                }
            }

            base.VisitFieldDeclaration(node);
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            IEventSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Event, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                RecordContainment(nodeId);
            }

            base.VisitEventDeclaration(node);
        }

        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            INamedTypeSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.Delegate, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                RecordContainment(nodeId);
            }

            base.VisitDelegateDeclaration(node);
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            IFieldSymbol? symbol = semanticModel.GetDeclaredSymbol(node);

            if (symbol != null)
            {
                long nodeId = RecordSymbol(symbol, node, CSharpSymbolKind.EnumMember, GetAccessibility(symbol), symbol.IsStatic, symbol.IsAbstract, symbol.IsVirtual, symbol.IsOverride, false, false);
                RecordContainment(nodeId);
            }

            base.VisitEnumMemberDeclaration(node);
        }

        private long RecordSymbol(ISymbol symbol, SyntaxNode node, CSharpSymbolKind kind, int? accessibility, bool isStatic, bool isAbstract, bool isVirtual, bool isOverride, bool isExtensionMethod, bool isAsync)
        {
            string fullyQualifiedName = GetFullyQualifiedName(symbol);
            string displayName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();
            TextSpan textSpan = node.Span;
            long? parentId = containerStack.Count > 0 ? containerStack.Peek() : null;

            long nodeId = repository.RecordNode(
                snapshotId,
                fullyQualifiedName,
                displayName,
                kind,
                parentId,
                accessibility,
                isStatic,
                isAbstract,
                isVirtual,
                isOverride,
                isExtensionMethod,
                isAsync);

            repository.RecordSourceLocation(
                nodeId,
                fileId,
                span.StartLinePosition.Line + 1,
                span.StartLinePosition.Character + 1,
                span.EndLinePosition.Line + 1,
                span.EndLinePosition.Character + 1,
                textSpan.Start,
                textSpan.End,
                LocationType.Definition);

            SymbolCount++;

            return nodeId;
        }

        private void RecordContainment(long childId)
        {
            if (containerStack.Count == 0)
            {
                return;
            }

            long containerId = containerStack.Peek();
            repository.RecordEdge(snapshotId, containerId, childId, CSharpReferenceKind.Contains);
        }

        private void RecordTypeRelationships(INamedTypeSymbol typeSymbol, long nodeId)
        {
            if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
            {
                string baseName = GetFullyQualifiedName(typeSymbol.BaseType);
                long baseId = repository.GetOrCreateNodeId(snapshotId, baseName);
                repository.RecordEdge(snapshotId, nodeId, baseId, CSharpReferenceKind.Inheritance);
            }

            foreach (INamedTypeSymbol interfaceSymbol in typeSymbol.Interfaces)
            {
                string interfaceName = GetFullyQualifiedName(interfaceSymbol);
                long interfaceId = repository.GetOrCreateNodeId(snapshotId, interfaceName);
                repository.RecordEdge(snapshotId, nodeId, interfaceId, CSharpReferenceKind.InterfaceImplementation);
            }
        }

        private void RecordMethodRelationships(IMethodSymbol methodSymbol, long nodeId)
        {
            if (methodSymbol.OverriddenMethod != null)
            {
                string baseName = GetFullyQualifiedName(methodSymbol.OverriddenMethod);
                long targetId = repository.GetOrCreateNodeId(snapshotId, baseName);
                repository.RecordEdge(snapshotId, nodeId, targetId, CSharpReferenceKind.Override);
            }

            foreach (IMethodSymbol interfaceMethod in methodSymbol.ExplicitInterfaceImplementations)
            {
                string interfaceName = GetFullyQualifiedName(interfaceMethod);
                long targetId = repository.GetOrCreateNodeId(snapshotId, interfaceName);
                repository.RecordEdge(snapshotId, nodeId, targetId, CSharpReferenceKind.InterfaceImplementation);
            }
        }

        private void RecordTypeUsage(ITypeSymbol typeSymbol, long sourceNodeId)
        {
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                string typeName = GetFullyQualifiedName(namedType);
                long typeId = repository.GetOrCreateNodeId(snapshotId, typeName);
                repository.RecordEdge(snapshotId, sourceNodeId, typeId, CSharpReferenceKind.TypeUsage);
            }
        }

        private static string GetFullyQualifiedName(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static int? GetAccessibility(ISymbol symbol)
        {
            return symbol.DeclaredAccessibility switch
            {
                Accessibility.Public => 0,
                Accessibility.Protected => 1,
                Accessibility.Internal => 2,
                Accessibility.ProtectedOrInternal => 3,
                Accessibility.Private => 4,
                Accessibility.ProtectedAndInternal => 5,
                _ => null
            };
        }
    }
}
