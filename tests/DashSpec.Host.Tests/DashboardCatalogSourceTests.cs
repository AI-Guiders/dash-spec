#nullable enable

using DashSpec.Host.Commands;
using DashSpec.Generated;
using Xunit;

namespace DashSpec.Host.Tests;

/// <summary>Phase I2/W1 — catalog SSOT is emit-only (ADR-0051 §4b I1–I2, DS-W1).</summary>
public sealed class DashboardCatalogSourceTests
{
    [Fact]
    public void Current_uses_generated_document_not_runtime_parse()
    {
        Assert.Same(DashCatalog.Document, DashboardCatalog.Current);
    }

    [Fact]
    public void Generated_surfaces_match_document_defaults()
    {
        var fromEmit = DashboardCatalog.FederationSurfaces;
        var fromDoc = DashboardCatalog.Current.Defaults.CommandSurfaces;

        Assert.Equal(fromDoc.Count, fromEmit.Count);
        foreach (var surface in fromEmit)
        {
            Assert.Contains(surface, fromDoc);
        }
    }

    [Fact]
    public void Generated_phrase_slots_index_covers_commands()
    {
        var slots = DashboardCatalog.PhraseSlots;
        foreach (var row in DashboardCatalog.Current.Commands)
        {
            Assert.True(
                slots.TryResolveCommand("", row.Command, out _),
                $"Missing phrase-slot emit for command '{row.Command}'.");
        }
    }

    [Fact]
    public void Bindings_and_flavor_resolve_from_generated_document()
    {
        var doc = DashboardCatalog.Current;

        Assert.Equal("Ctrl+K", doc.Defaults.BindingChordRoot);
        Assert.Equal("console", doc.Defaults.CommandFlavor);
        Assert.Equal("Ctrl+K", DashboardCatalogBindings.ChordRootGesture);
        Assert.Equal("Ctrl+.", DashboardCatalogBindings.SuggestDismissGesture);
    }
}
