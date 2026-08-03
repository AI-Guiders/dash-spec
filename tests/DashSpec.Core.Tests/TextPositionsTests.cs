using DashSpec.Core.Validation;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class TextPositionsTests
{
    [Fact]
    public void GetLineColumn_FirstLine()
    {
        const string text = "diagram foo\nbar";
        var (line, col) = TextPositions.GetLineColumn(text, 8);
        Assert.Equal(0, line);
        Assert.Equal(8, col);
    }

    [Fact]
    public void GetLineColumn_SecondLine()
    {
        const string text = "diagram foo\nbar";
        var (line, col) = TextPositions.GetLineColumn(text, 13);
        Assert.Equal(1, line);
        Assert.Equal(1, col);
    }

    [Fact]
    public void ToDiagnostic_UsesSourceOffset()
    {
        const string text = "line one\nbad token here";
        var diagnostic = TextPositions.ToDiagnostic(text, "Unexpected token", 9);
        Assert.Equal(1, diagnostic.Line);
        Assert.Equal(0, diagnostic.Character);
    }

    [Fact]
    public void ToDiagnostic_ParsesPositionFromMessage()
    {
        const string text = "abcdefghij";
        var diagnostic = TextPositions.ToDiagnostic(text, "Unexpected character 'x' at position 3.", null);
        Assert.Equal(0, diagnostic.Line);
        Assert.Equal(3, diagnostic.Character);
    }
}
