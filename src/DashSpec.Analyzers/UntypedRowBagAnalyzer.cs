using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DashSpec.Analyzers;

/// <summary>ADR-0085 A1: untyped row bags forbidden outside connector acquisition.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UntypedRowBagAnalyzer : DiagnosticAnalyzer
{
    public const string UntypedRowBagId = "DSPEC030";

    private const string UntypedRowDictionaryName = "IReadOnlyDictionary";
    private const string UntypedRowMutableDictionaryName = "Dictionary";

    private static readonly DiagnosticDescriptor Rule = new(
        UntypedRowBagId,
        "Untyped row dictionaries are forbidden outside connectors",
        "Use TypedRowBatch / TypedDataRow from DashSpec.Abstractions.Data instead of Dictionary<string, object?> for query rows (ADR-0087)",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeTypeSyntax, SyntaxKind.GenericName);
    }

    private static void AnalyzeTypeSyntax(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not GenericNameSyntax generic)
        {
            return;
        }

        var path = context.Node.SyntaxTree.FilePath;
        if (DashSpecLayerPaths.IsConnectorAcquisitionLayer(path))
        {
            return;
        }

        if (!DashSpecLayerPaths.IsUntypedRowBagForbiddenLayer(path))
        {
            return;
        }

        if (!IsUntypedRowBag(generic))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, generic.GetLocation()));
    }

    private static bool IsUntypedRowBag(GenericNameSyntax generic)
    {
        var name = generic.Identifier.ValueText;
        if (name is not UntypedRowDictionaryName and not UntypedRowMutableDictionaryName)
        {
            return false;
        }

        var args = generic.TypeArgumentList?.Arguments;
        if (args is null || args.Value.Count != 2)
        {
            return false;
        }

        var key = args.Value[0];
        var value = args.Value[1];

        if (key is not PredefinedTypeSyntax keyPredefined ||
            keyPredefined.Keyword.ValueText is not "string")
        {
            return false;
        }

        if (value is not NullableTypeSyntax nullable ||
            nullable.ElementType is not PredefinedTypeSyntax valuePredefined ||
            valuePredefined.Keyword.ValueText is not "object")
        {
            return false;
        }

        return true;
    }
}
