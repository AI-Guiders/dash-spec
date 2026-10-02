using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DashSpec.Analyzers;

/// <summary>ADR-0085: SQL and external I/O only in connector acquisition layer.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DataAcquisitionIoBoundaryAnalyzer : DiagnosticAnalyzer
{
    public const string ForbiddenSqlClientId = "DSPEC020";
    public const string ForbiddenExternalIoId = "DSPEC021";

    private static readonly ImmutableHashSet<string> ForbiddenSqlTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "SqlConnection",
        "SqlCommand",
        "SqlDataReader",
        "SqlParameter",
        "SqlBulkCopy",
        "SqlTransaction",
        "NpgsqlConnection",
        "NpgsqlCommand",
        "NpgsqlDataReader");

    private static readonly ImmutableHashSet<string> ForbiddenExternalTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "File",
        "Directory",
        "FileInfo",
        "DirectoryInfo",
        "Process",
        "HttpClient");

    private static readonly DiagnosticDescriptor ForbiddenSqlRule = new(
        ForbiddenSqlClientId,
        "SQL client types belong in connector acquisition",
        "In '{0}' direct SQL client access via '{1}' is forbidden — use IDataSourceConnector in connectors/ (ADR-0085)",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ForbiddenExternalIoRule = new(
        ForbiddenExternalIoId,
        "External I/O belongs in acquisition or Host infrastructure",
        "In Execution.Runtime direct access via '{0}' is forbidden — keep I/O in connectors/ or Host (ADR-0085)",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(ForbiddenSqlRule, ForbiddenExternalIoRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (memberAccess.Expression is not IdentifierNameSyntax receiver)
        {
            return;
        }

        var typeName = receiver.Identifier.ValueText;
        ReportIfForbidden(context, typeName, memberAccess.GetLocation());
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ObjectCreationExpressionSyntax creation)
        {
            return;
        }

        var typeName = ExtractSimpleTypeName(creation.Type);
        if (typeName is null)
        {
            return;
        }

        ReportIfForbidden(context, typeName, creation.GetLocation());
    }

    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not IdentifierNameSyntax identifier)
        {
            return;
        }

        if (identifier.Parent is ObjectCreationExpressionSyntax or MemberAccessExpressionSyntax)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
        if (symbol is INamedTypeSymbol named)
        {
            ReportIfForbidden(context, named.Name, identifier.GetLocation());
        }
    }

    private static void ReportIfForbidden(SyntaxNodeAnalysisContext context, string typeName, Location location)
    {
        var path = context.Node.SyntaxTree.FilePath;
        if (DashSpecLayerPaths.IsConnectorAcquisitionLayer(path))
        {
            return;
        }

        if (ForbiddenSqlTypeNames.Contains(typeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ForbiddenSqlRule,
                location,
                LayerLabel(path),
                typeName));
            return;
        }

        if (!DashSpecLayerPaths.IsExecutionRuntimeLayer(path))
        {
            return;
        }

        if (ForbiddenExternalTypeNames.Contains(typeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ForbiddenExternalIoRule,
                location,
                typeName));
        }
    }

    private static string? ExtractSimpleTypeName(TypeSyntax typeSyntax) =>
        typeSyntax switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => null,
        };

    private static string LayerLabel(string path)
    {
        if (DashSpecLayerPaths.IsExecutionRuntimeLayer(path))
        {
            return "Execution.Runtime";
        }

        if (DashSpecLayerPaths.IsHostProject(path))
        {
            return "Host";
        }

        return "this layer";
    }
}
