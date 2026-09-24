#nullable enable

using DashSpec.Generated;
using DashSpec.Host.Commands;
using Xunit;

namespace DashSpec.Host.Tests;

/// <summary>Phase I2 — catalog SSOT is emit-only (ADR-0051 §4b I1–I2, DS-W1).</summary>
public sealed class DashCatalogSourceTests
{
    [Fact]
    public void Document_is_emit_ssot_with_load_validation()
    {
        Assert.NotNull(DashCatalog.Document);
        Assert.Equal("dash", DashCatalog.Document.Planet);
    }

    [Fact]
    public void Generated_surfaces_match_document_defaults()
    {
        var fromEmit = DashCatalog.FederationSurfaces;
        var fromDoc = DashCatalog.Document.Defaults.CommandSurfaces;

        Assert.Equal(fromDoc.Count, fromEmit.Length);
        foreach (var surface in fromEmit)
        {
            Assert.Contains(surface, fromDoc);
        }
    }

    [Fact]
    public void Generated_phrase_slots_index_covers_commands()
    {
        var slots = DashCatalog.PhraseSlots;
        foreach (var row in DashCatalog.Document.Commands)
        {
            Assert.True(
                slots.TryResolveCommand("", row.Command, out _),
                $"Missing phrase-slot emit for command '{row.Command}'.");
        }
    }

    [Fact]
    public void Bindings_and_flavor_resolve_from_generated_document()
    {
        var doc = DashCatalog.Document;

        Assert.Equal("Ctrl+K", doc.Defaults.BindingChordRoot);
        Assert.Equal("console", doc.Defaults.CommandFlavor);
        Assert.Equal("Ctrl+K", DashboardCatalogBindings.ChordRootGesture);
        Assert.Equal("Ctrl+.", DashboardCatalogBindings.SuggestDismissGesture);
    }
}
