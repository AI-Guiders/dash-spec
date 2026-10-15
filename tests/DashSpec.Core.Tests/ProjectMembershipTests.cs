using System;
using System.IO;
using System.Linq;
using DashSpec.Modeling.Parse.Project;
using Xunit;

namespace DashSpec.Core.Tests;

public class ProjectMembershipTests
{
    private static string NewRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dsproj-membership-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "diagrams", "_scratch"));
        File.WriteAllText(Path.Combine(dir, "diagrams", "a.dashdiagram"), "module diagram a" + Environment.NewLine + "end diagram");
        File.WriteAllText(Path.Combine(dir, "diagrams", "b.dashdiagram"), "module diagram b" + Environment.NewLine + "end diagram");
        File.WriteAllText(Path.Combine(dir, "diagrams", "_scratch", "tmp.dashdiagram"), "module diagram tmp" + Environment.NewLine + "end diagram");
        File.WriteAllText(Path.Combine(dir, "rows.dashtype"), "module types rows" + Environment.NewLine + "type T" + Environment.NewLine + "end type" + Environment.NewLine + "end types");
        return dir;
    }

    private static DashSpecProjectConfig Config(string root, string[]? files = null, string[]? exclude = null) =>
        new(
            root,
            null,
            new[] { "." },
            true,
            files ?? new[] { "diagrams/*.dashdiagram", "rows.dashtype" },
            exclude ?? new[] { "diagrams/_scratch/*" });

    [Fact]
    public void Expand_is_lex_sorted_and_excludes_apply_after_positives()
    {
        var root = NewRoot();
        var universe = DashSpecProjectMembership.expand(Config(root)).Select(Path.GetFileName).ToArray();

        Assert.Equal(new[] { "a.dashdiagram", "b.dashdiagram", "rows.dashtype" }, universe);
    }

    [Fact]
    public void IsDeclared_gates_paths_against_membership()
    {
        var root = NewRoot();
        var cfg = Config(root);

        Assert.True(DashSpecProjectMembership.isDeclared(cfg, Path.Combine(root, "diagrams", "a.dashdiagram")));
        Assert.False(DashSpecProjectMembership.isDeclared(cfg, Path.Combine(root, "diagrams", "_scratch", "tmp.dashdiagram")));
        Assert.False(DashSpecProjectMembership.isDeclared(cfg, Path.Combine(root, "other.dashdiagram")));
    }

    [Fact]
    public void IsDeclared_is_unconstrained_without_files()
    {
        var root = NewRoot();
        var cfg = Config(root, Array.Empty<string>(), Array.Empty<string>());

        Assert.True(DashSpecProjectMembership.isDeclared(cfg, Path.Combine(root, "anywhere.dashdiagram")));
    }
}
