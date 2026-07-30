using DocNative.Core.Validation;

namespace DocNative.Core.Tests;

public class InterleavedPdfAnalyzerTests
{
    [Fact]
    public void Check_SingleOperationContiguous_ReturnsOk()
    {
        var map = new Dictionary<int, string>
        {
            [1] = "0813601000",
            [2] = "0813601000",
            [3] = "0813601000",
        };

        var result = InterleavedPdfAnalyzer.Check(map, 3);
        Assert.False(result.IsInterleaved);
    }

    [Fact]
    public void Check_TwoOperationsSequential_ReturnsOk()
    {
        var map = new Dictionary<int, string>
        {
            [1] = "0813601000",
            [2] = "0813601000",
            [3] = "0813602000",
            [4] = "0813602000",
        };

        var result = InterleavedPdfAnalyzer.Check(map, 4);
        Assert.False(result.IsInterleaved);
    }

    [Fact]
    public void Check_SameCodeInNonContiguousBlocks_ReturnsFail()
    {
        var map = new Dictionary<int, string>
        {
            [1] = "0813601000",
            [2] = "0813601000",
            [3] = "0813602000",
            [4] = "0813602000",
            [5] = "0813602000",
            [6] = "0813602000",
            [7] = "0813601000",
            [8] = "0813601000",
            [9] = "0813601000",
        };

        var result = InterleavedPdfAnalyzer.Check(map, 9);
        Assert.True(result.IsInterleaved);
        Assert.Contains("PDF mal ordenado", result.Message, StringComparison.Ordinal);
        Assert.Contains("0813601000", result.Message, StringComparison.Ordinal);
        Assert.Contains("0813602000", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardFill_AssignsPreviousCodeToBlankPages()
    {
        var detected = new Dictionary<int, string>
        {
            [1] = "0813601000",
            [3] = "0813602000",
        };

        var filled = InterleavedPdfAnalyzer.ForwardFill(detected, 4);

        Assert.Equal("0813601000", filled[1]);
        Assert.Equal("0813601000", filled[2]);
        Assert.Equal("0813602000", filled[3]);
        Assert.Equal("0813602000", filled[4]);
    }
}
