using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DashSpec.Analyzers;

/// <summary>ADR-0087: DSPEC031–034 typed row wire enforcement.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypedRowWireAnalyzer : DiagnosticAnalyzer
{
    public const string UntypedObjectCellArrayId = "DSPEC031";
    public const string LegacyRowBatchCreateId = "DSPEC032";
    public const string UntypedDataRowAccessId = "DSPEC033";
    public const string LegacyRowBatchTypeId = "DSPEC034";

    private static readonly DiagnosticDescriptor UntypedObjectCellArrayRule = new(
        UntypedObjectCellArrayId,
        "Untyped object cell arrays are forbidden in the data plane",
        "Use DashValue[] only inside acquisition materialization (connectors/ or Abstractions/Data/Acquisition/)",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor LegacyRowBatchCreateRule = new(
        LegacyRowBatchCreateId,
        "Legacy RowBatch materialization is forbidden",
        "Use TypedRowBatch and SqlRowMaterializer in acquisition layers only (ADR-0087)",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UntypedDataRowAccessRule = new(
        UntypedDataRowAccessId,
        "Untyped DataRow access is forbidden",
        "Use TypedDataRow and DashValue getters instead of DataRow.GetValueOrDefault (ADR-0087)",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor LegacyRowBatchTypeRule = new(
        LegacyRowBatchTypeId,
        "Legacy RowBatch / DataRow types are forbidden in the data plane",
        "Use TypedRowBatch / TypedDataRow (ADR-0087)",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            UntypedObjectCellArrayRule,
            LegacyRowBatchCreateRule,
            UntypedDataRowAccessRule,
            LegacyRowBatchTypeRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeArrayType, SyntaxKind.ArrayType);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeArrayType(SyntaxNodeAnalysisContext context)
    {
        if (!DashSpecLayerPaths.IsDataPlaneLayer(context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        if (context.Node is not ArrayTypeSyntax arrayType)
        {
            return;
        }

        if (arrayType.ElementType is not NullableTypeSyntax nullable
            || nullable.ElementType is not PredefinedTypeSyntax predefined
            || predefined.Keyword.ValueText is not "object")
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(UntypedObjectCellArrayRule, arrayType.GetLocation()));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (!DashSpecLayerPaths.IsDataPlaneLayer(context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Create", StringComparison.Ordinal))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol as IMethodSymbol;
        if (symbol?.ContainingType.Name is not "RowBatch")
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LegacyRowBatchCreateRule, invocation.GetLocation()));
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        if (!DashSpecLayerPaths.IsDataPlaneLayer(context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        if (context.Node is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var name = memberAccess.Name.Identifier.ValueText;
        if (name is not "GetValueOrDefault" and not "TryGetValue")
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol as IMethodSymbol;
        if (symbol?.ContainingType.Name is not "DataRow")
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(UntypedDataRowAccessRule, memberAccess.GetLocation()));
    }

    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        if (!DashSpecLayerPaths.IsDataPlaneLayer(context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        if (context.Node is not IdentifierNameSyntax identifier)
        {
            return;
        }

        if (identifier.Parent is MemberAccessExpressionSyntax member && member.Name == identifier)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
        if (symbol is INamedTypeSymbol named && named.Name is "RowBatch" or "DataRow")
        {
            context.ReportDiagnostic(Diagnostic.Create(LegacyRowBatchTypeRule, identifier.GetLocation()));
        }
    }
}
