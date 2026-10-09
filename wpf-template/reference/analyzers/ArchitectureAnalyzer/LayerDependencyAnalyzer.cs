using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WpfNotesSample.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LayerDependencyAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] _layers = { "View", "ViewModel", "Domain", "Infrastructure" };

    private static readonly DiagnosticDescriptor _dependencyRule = new(
        "ARCH001", "Layer dependency is not allowed", "{0} cannot reference {1}",
        "Architecture", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _legacyRule = new(
        "ARCH002", "Legacy Model namespace is not allowed", "Type '{0}' must not be declared under {1}.Model",
        "Architecture", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(_dependencyRule, _legacyRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeDeclaration, SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(AnalyzeReference, SyntaxKind.IdentifierName, SyntaxKind.GenericName,
            SyntaxKind.InvocationExpression, SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void AnalyzeDeclaration(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var root = context.Compilation.AssemblyName;
        if (root is null || !IsNamespace(type.ContainingNamespace.ToDisplayString(), root + ".Model"))
        {
            return;
        }

        foreach (var location in type.Locations.Where(location => location.IsInSource))
        {
            context.ReportDiagnostic(Diagnostic.Create(_legacyRule, location, type.Name, root));
        }
    }

    private static void AnalyzeReference(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.Ancestors().Any(node => node is UsingDirectiveSyntax))
        {
            return;
        }

        var declaration = context.Node.Ancestors().FirstOrDefault(node =>
            node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);
        var source = declaration is null ? null : context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
        var sourceLayer = GetLayer(source?.ContainingNamespace.ToDisplayString(), context.Compilation.AssemblyName);
        if (sourceLayer is null)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol;
        var types = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        AddSymbolTypes(symbol, types);
        var typeInfo = context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken);
        AddType(typeInfo.Type, types);
        AddType(typeInfo.ConvertedType, types);
        var reportedLayers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            ReportDependency(context, sourceLayer, type, reportedLayers);
        }
    }

    private static void ReportDependency(SyntaxNodeAnalysisContext context, string sourceLayer, ITypeSymbol type,
        HashSet<string> reportedLayers)
    {
        if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, context.Compilation.Assembly))
        {
            return;
        }

        var targetLayer = GetLayer(type.ContainingNamespace?.ToDisplayString(), context.Compilation.AssemblyName);
        if (targetLayer is not null && !IsAllowed(sourceLayer, targetLayer) && reportedLayers.Add(targetLayer))
        {
            context.ReportDiagnostic(Diagnostic.Create(_dependencyRule, context.Node.GetLocation(), sourceLayer, targetLayer));
        }
    }

    private static void AddSymbolTypes(ISymbol? symbol, HashSet<ITypeSymbol> types)
    {
        AddType(symbol?.ContainingType, types);
        switch (symbol)
        {
            case ITypeSymbol type:
                AddType(type, types);
                break;
            case IAliasSymbol alias:
                AddSymbolTypes(alias.Target, types);
                break;
            case IMethodSymbol method:
                AddMethodTypes(method, types);
                break;
            case IPropertySymbol property:
                AddType(property.Type, types);
                break;
            case IFieldSymbol field:
                AddType(field.Type, types);
                break;
            case IEventSymbol eventSymbol:
                AddType(eventSymbol.Type, types);
                break;
            case ILocalSymbol local:
                AddType(local.Type, types);
                break;
            case IParameterSymbol parameter:
                AddType(parameter.Type, types);
                break;
            default:
                return;
        }
    }

    private static void AddMethodTypes(IMethodSymbol method, HashSet<ITypeSymbol> types)
    {
        AddType(method.ReturnType, types);
        foreach (var argument in method.TypeArguments)
        {
            AddType(argument, types);
        }

        foreach (var parameter in method.Parameters)
        {
            AddType(parameter.Type, types);
        }

        AddSymbolTypes(method.ReducedFrom, types);
    }

    private static void AddType(ITypeSymbol? type, HashSet<ITypeSymbol> types)
    {
        if (type is null || !types.Add(type))
        {
            return;
        }

        switch (type)
        {
            case INamedTypeSymbol named:
                AddType(named.ContainingType, types);
                foreach (var argument in named.TypeArguments)
                {
                    AddType(argument, types);
                }

                break;
            case IArrayTypeSymbol array:
                AddType(array.ElementType, types);
                break;
            case IPointerTypeSymbol pointer:
                AddType(pointer.PointedAtType, types);
                break;
            default:
                return;
        }
    }

    private static string? GetLayer(string? namespaceName, string? root)
    {
        if (namespaceName is null || root is null)
        {
            return null;
        }

        return Array.Find(_layers, layer => IsNamespace(namespaceName, root + "." + layer));
    }

    private static bool IsNamespace(string namespaceName, string prefix) =>
        namespaceName.Equals(prefix, StringComparison.Ordinal) || namespaceName.StartsWith(prefix + ".", StringComparison.Ordinal);

    private static bool IsAllowed(string source, string target)
    {
        if (source == target)
        {
            return true;
        }

        return source switch
        {
            "View" => target == "ViewModel",
            "ViewModel" or "Infrastructure" => target == "Domain",
            _ => false,
        };
    }
}
