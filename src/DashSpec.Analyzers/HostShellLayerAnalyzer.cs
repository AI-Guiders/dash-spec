using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DashSpec.Analyzers;

/// <summary>ADR-0074: Host shell must not build payloads or format data outside rendering/bootstrap.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HostShellLayerAnalyzer : DiagnosticAnalyzer
{
    public const string PayloadOutsideRenderingId = "DSCHOST001";
    public const string LabelFormatOutsideRenderingId = "DSCHOST002";
    public const string DateCodecInUiId = "DSCHOST003";

    private static readonly DiagnosticDescriptor PayloadRule = new(
        PayloadOutsideRenderingId,
        "Chart/matrix payloads belong in Services.Rendering",
        "Host must not reference '{0}' outside Services/Rendering",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor LabelFormatRule = new(
        LabelFormatOutsideRenderingId,
        "Label formatting belongs in Execution.Runtime",
        "Host must not reference LabelFormat outside allowed bootstrap paths",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DateCodecRule = new(
        DateCodecInUiId,
        "Date parsing belongs in Core.Runtime / Execution.Runtime",
        "Host UI must not reference DateValueCodec in presentation or components",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PayloadRule, LabelFormatRule, DateCodecRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not IdentifierNameSyntax identifier)
        {
            return;
        }

        var path = context.Node.SyntaxTree.FilePath;
        if (!DashSpecLayerPaths.IsHostProject(path))
        {
            return;
        }

        var name = identifier.Identifier.Text;
        switch (name)
        {
            case "ChartDataBuilder" or "MatrixPayloadBuilder" or "AxisLabelSort":
                if (!IsUnderRendering(path))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        PayloadRule,
                        identifier.GetLocation(),
                        name));
                }

                break;

            case "LabelFormat":
                if (!IsLabelFormatAllowed(path))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        LabelFormatRule,
                        identifier.GetLocation()));
                }

                break;

            case "DateValueCodec":
                if (IsUnderComponents(path) || IsUnderPresentation(path))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DateCodecRule,
                        identifier.GetLocation(),
                        RelativeHostPath(path)));
                }

                break;
        }
    }

    private static bool IsUnderRendering(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}Rendering{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnderComponents(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}Components{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnderPresentation(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}Presentation{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static bool IsLabelFormatAllowed(string path) =>
        IsUnderRendering(path)
        || path.EndsWith($"{Path.DirectorySeparatorChar}Program.cs", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}Configuration{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}Diagnostics{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string RelativeHostPath(string path)
    {
        var marker = $"{Path.DirectorySeparatorChar}DashSpec.Host{Path.DirectorySeparatorChar}";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? path : path.Substring(index + marker.Length);
    }
}
