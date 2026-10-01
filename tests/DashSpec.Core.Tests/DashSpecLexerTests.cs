using DashSpec.Core.Parsing;
using DashSpec.Modeling.Core;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecLexerTests
{
    [Fact]
    public void Line_comment_slash_slash_at_line_start_is_skipped()
    {
        var tokens = DashSpecLexer.tokenize("""
            // C++-style comment
            title = "T"
            """);

        Assert.DoesNotContain(tokens, t => t.Kind == TokenKind.Ident && t.Value == "C++-style");
        Assert.Contains(tokens, t => t.Kind == TokenKind.String && t.Value == "T");
    }

    [Fact]
    public void Hash_at_line_start_is_hex_not_comment()
    {
        var tokens = DashSpecLexer.tokenize("""
            #e11d48
            title = "T"
            """);

        Assert.Contains(tokens, t => t.Kind == TokenKind.HexColor && t.Value == "#e11d48");
    }

    [Fact]
    public void Hash_text_at_line_start_is_invalid_hex()
    {
        var ex = Assert.Throws<DashSpec.Modeling.Core.DashSpecParseException>(() => DashSpecLexer.tokenize("# not a comment\n"));
        Assert.Contains("Invalid hex color", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bare_hex_mid_line_is_tokenized()
    {
        var tokens = DashSpecLexer.tokenize("colors = [#e11d48, #fff]");

        var hex = tokens.Where(t => t.Kind == TokenKind.HexColor).Select(t => t.Value).ToList();
        Assert.Equal(["#e11d48", "#fff"], hex);
    }

    [Fact]
    public void Quoted_string_preserves_hash()
    {
        var tokens = DashSpecLexer.tokenize("color = \"#e11d48\"");

        Assert.Contains(tokens, t => t.Kind == TokenKind.String && t.Value == "#e11d48");
        Assert.DoesNotContain(tokens, t => t.Kind == TokenKind.HexColor);
    }

    [Fact]
    public void Multiline_string_preserves_hash_and_slashes_inside()
    {
        var tokens = DashSpecLexer.tokenize("note = \"\"\"\n# not a comment inside multiline\n// also preserved\n\"\"\"");

        var value = Assert.Single(tokens, t => t.Kind == TokenKind.String).Value;
        Assert.Contains("# not a comment", value, StringComparison.Ordinal);
        Assert.Contains("// also preserved", value, StringComparison.Ordinal);
    }

    [Fact]
    public void Block_comment_spans_lines()
    {
        var tokens = DashSpecLexer.tokenize("""
            /*
              ADR note
              # hash inside block
              // slashes inside block
            */
            title = "T"
            """);

        Assert.DoesNotContain(tokens, t => t.Kind == TokenKind.Ident && t.Value == "ADR");
        Assert.Contains(tokens, t => t.Kind == TokenKind.String && t.Value == "T");
    }

    [Fact]
    public void Block_comment_mid_line_before_tokens()
    {
        var tokens = DashSpecLexer.tokenize("colors = [ /* cycle */ #e11d48]");

        Assert.Contains(tokens, t => t.Kind == TokenKind.HexColor && t.Value == "#e11d48");
    }

    [Fact]
    public void Unterminated_block_comment_throws()
    {
        var ex = Assert.Throws<DashSpec.Modeling.Core.DashSpecParseException>(() => DashSpecLexer.tokenize("/* oops"));
        Assert.Contains("block comment", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
