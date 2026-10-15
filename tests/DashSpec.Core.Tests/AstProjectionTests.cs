using System.Collections.Generic;
using System.Linq;
using DashSpec.Modeling.Parse.Syntax;
using Xunit;

namespace DashSpec.Core.Tests;

public class AstProjectionTests
{
    private const string Sample =
        """
        module dashboard sample
          import types from Sample.Types as rows
          import layouts from Sample.Layouts

          runtime
            manifest = "x.toml"
          end runtime

          report
            title = "T"
          end report
        end dashboard
        """;

    private static string Digest(ParseTree tree)
    {
        var parts = new List<string>();

        void Walk(DashSpecAstNode node)
        {
            parts.Add(DashSpecAst.nodeKind(node).ToString());

            foreach (var child in DashSpecAst.members(node))
            {
                Walk(child);
            }
        }

        Walk(tree.Root);
        return string.Join("|", parts);
    }

    [Fact]
    public void Parse_emits_module_unit_and_import_nodes()
    {
        var tree = DashSpecSurfaceSyntax.parse(Sample);
        var root = DashSpecAst.members(tree.Root);

        Assert.Equal(DashSpecAstNodeKind.ModuleDeclaration, DashSpecAst.nodeKind(root[0]));
        Assert.Equal("module dashboard sample", DashSpecAst.outlineLabel(root[0]));

        var moduleMembers = DashSpecAst.members(root[0]);
        Assert.Equal(
            new[]
            {
                DashSpecAstNodeKind.ImportDeclaration,
                DashSpecAstNodeKind.ImportDeclaration,
                DashSpecAstNodeKind.BlockDeclaration,
                DashSpecAstNodeKind.BlockDeclaration,
                DashSpecAstNodeKind.EndBlock
            },
            moduleMembers.Select(DashSpecAst.nodeKind).ToArray());
    }

    [Fact]
    public void Parse_is_deterministic_and_spans_cover_the_text()
    {
        var first = DashSpecSurfaceSyntax.parse(Sample);
        var second = DashSpecSurfaceSyntax.parse(Sample);

        Assert.Equal(Digest(first), Digest(second));
        Assert.True(DashSpecAst.span(first.Root).End > DashSpecAst.span(first.Root).Start);
    }
}
