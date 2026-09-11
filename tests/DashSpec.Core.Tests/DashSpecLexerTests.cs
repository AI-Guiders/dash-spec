using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecLexerTests
{
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
    public void Hash_at_line_start_is_hex_not_comment()
    {
        var tokens = DashSpecLexer.Tokenize("""
            #e11d48
            title = "T"
            """);

        Assert.Contains(tokens, t => t.Kind is TokenKind.HexColor && t.Value == "#e11d48");
    }

    [Fact]
    public void Hash_text_at_line_start_is_invalid_hex()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecLexer.Tokenize("# not a comment\n"));
        Assert.Contains("Invalid hex color", ex.Message, StringComparison.Ordinal);
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
    public void Multiline_string_preserves_hash_and_slashes_inside()
    {
        var tokens = DashSpecLexer.Tokenize("note = \"\"\"\n# not a comment inside multiline\n// also preserved\n\"\"\"");

        var value = Assert.Single(tokens, t => t.Kind is TokenKind.String).Value;
        Assert.Contains("# not a comment", value, StringComparison.Ordinal);
        Assert.Contains("// also preserved", value, StringComparison.Ordinal);
    }

    [Fact]
    public void Block_comment_spans_lines()
    {
        var tokens = DashSpecLexer.Tokenize("""
            /*
              ADR note
              # hash inside block
              // slashes inside block
            */
            title = "T"
            """);

        Assert.DoesNotContain(tokens, t => t.Kind is TokenKind.Ident && t.Value == "ADR");
        Assert.Contains(tokens, t => t.Kind is TokenKind.String && t.Value == "T");
    }

    [Fact]
    public void Block_comment_mid_line_before_tokens()
    {
        var tokens = DashSpecLexer.Tokenize("colors = [ /* cycle */ #e11d48]");

        Assert.Contains(tokens, t => t.Kind is TokenKind.HexColor && t.Value == "#e11d48");
    }

    [Fact]
    public void Unterminated_block_comment_throws()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecLexer.Tokenize("/* oops"));
        Assert.Contains("block comment", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
