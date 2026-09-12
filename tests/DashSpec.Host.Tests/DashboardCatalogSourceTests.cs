#nullable enable

using DashSpec.Host.Commands;
using Xunit;

namespace DashSpec.Host.Tests;

/// <summary>Phase I2 — documents generated vs runtime-parse catalog split (ADR-0051 §4b I2).</summary>
public sealed class DashboardCatalogSourceTests
{
    [Fact]
    public void Generated_surfaces_match_runtime_defaults()
    {
        var fromEmit = DashboardCatalog.FederationSurfaces;
        var fromGdl = DashboardCatalog.Current.Defaults.CommandSurfaces;

        Assert.Equal(fromGdl.Count, fromEmit.Length);
        foreach (var surface in fromEmit)
        {
            Assert.Contains(surface, fromGdl);
        }
    }

    [Fact]
    public void Generated_phrase_slots_index_covers_runtime_commands()
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
    public void Bindings_and_flavor_resolve_from_runtime_parse_not_hardcoded_gestures()
    {
        var doc = DashboardCatalog.Current;

        Assert.Equal("Ctrl+K", doc.Defaults.BindingChordRoot);
        Assert.Equal("console", doc.Defaults.CommandFlavor);
        Assert.Equal("Ctrl+K", DashboardCatalogBindings.ChordRootGesture);
        Assert.Equal("Ctrl+.", DashboardCatalogBindings.SuggestDismissGesture);
    }
}
