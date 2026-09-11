using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecLexerTests
{
    [Fact]
    public void Line_comment_hash_at_line_start_is_skipped()
    {
        var tokens = DashSpecLexer.Tokenize("""
            # full-line comment
            title = "T"
            """);

        Assert.DoesNotContain(tokens, t => t.Kind is TokenKind.Ident && t.Value == "full-line");
        Assert.Contains(tokens, t => t.Kind is TokenKind.String && t.Value == "T");
    }

    [Fact]
    public void Line_comment_slash_slash_at_line_start_is_skipped()
    {
        var tokens = DashSpecLexer.Tokenize("""
            // C++-style comment
            title = "T"
            """);

        Assert.DoesNotContain(tokens, t => t.Kind is TokenKind.Ident && t.Value == "C++-style");
        Assert.Contains(tokens, t => t.Kind is TokenKind.String && t.Value == "T");
    }

    [Fact]
    public void Hash_at_line_start_is_comment_even_for_hex_shape()
    {
        var tokens = DashSpecLexer.Tokenize("""
            #e11d48 looks like hex but is a line comment
            title = "T"
            """);

        Assert.DoesNotContain(tokens, t => t.Kind is TokenKind.HexColor);
    }

    [Fact]
    public void Bare_hex_mid_line_is_tokenized()
    {
        var tokens = DashSpecLexer.Tokenize("colors = [#e11d48, #fff]");

        var hex = tokens.Where(t => t.Kind is TokenKind.HexColor).Select(t => t.Value).ToList();
        Assert.Equal(["#e11d48", "#fff"], hex);
    }

    [Fact]
    public void Quoted_string_preserves_hash()
    {
        var tokens = DashSpecLexer.Tokenize("color = \"#e11d48\"");

        Assert.Contains(tokens, t => t.Kind is TokenKind.String && t.Value == "#e11d48");
        Assert.DoesNotContain(tokens, t => t.Kind is TokenKind.HexColor);
    }

    [Fact]
    public void Multiline_string_preserves_line_comments_inside()
    {
        var tokens = DashSpecLexer.Tokenize("note = \"\"\"\n# not a comment inside multiline\n// also preserved\n\"\"\"");

        var value = Assert.Single(tokens.Where(t => t.Kind is TokenKind.String)).Value;
        Assert.Contains("# not a comment", value, StringComparison.Ordinal);
        Assert.Contains("// also preserved", value, StringComparison.Ordinal);
    }
}
