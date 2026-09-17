using AIGuiders.Surface.Wpf.Abstractions;
using FsharpSession = DashSpec.Modeling.LanguageEditor;
using Microsoft.FSharp.Core;

namespace DashSpec.Execution.LanguageEditor;

/// <summary>F# document session exposed to Surface text engine (model-first).</summary>
public sealed class DashSpecLanguageDocumentSession : ILanguageDocumentSession
{
    readonly FsharpSession.DocumentSession _inner;

    public DashSpecLanguageDocumentSession(string documentId, string text)
    {
        _inner = FsharpSession.DocumentSession.Create(documentId, text);
    }

    public int Revision => _inner.Revision;

    public string Text => _inner.Text;

    public string DocumentId => _inner.DocumentId;

    public void SyncFromText(string text) => _inner.SyncFromText(text);

    public IReadOnlyList<LanguageClassificationSpan> GetClassificationSpans() =>
        _inner.GetClassificationSpans()
            .Select(span => new LanguageClassificationSpan(span.Start, span.Length, span.Kind))
            .ToArray();

    public bool TryResolveCaret(int offset, out LanguageLocus locus)
    {
        locus = default;
        var maybe = _inner.TryResolveCaret(offset);
        if (!FSharpOption<FsharpSession.SessionLocus>.get_IsSome(maybe))
        {
            return false;
        }

        var found = maybe.Value;
        locus = new LanguageLocus(
            found.Start,
            found.End,
            ParseTier(found.Tier),
            OptionModule.ToObj(found.SymbolId));
        return true;
    }

    static LanguageResolveTier ParseTier(string tier) =>
        tier.Equals("Semantic", StringComparison.OrdinalIgnoreCase)
            ? LanguageResolveTier.Semantic
            : tier.Equals("Syntax", StringComparison.OrdinalIgnoreCase)
                ? LanguageResolveTier.Syntax
                : LanguageResolveTier.Text;
}
