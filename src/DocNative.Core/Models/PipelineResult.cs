namespace DocNative.Core.Models;

public sealed class PipelineResult
{
    public bool Success { get; init; }

    public string? OutputPath { get; init; }

    public string? ErrorMessage { get; init; }

    public int PagesRemoved { get; init; }

    public int PagesRotated { get; init; }

    public static PipelineResult Ok(string outputPath, int pagesRemoved, int pagesRotated) =>
        new()
        {
            Success = true,
            OutputPath = outputPath,
            PagesRemoved = pagesRemoved,
            PagesRotated = pagesRotated
        };

    public static PipelineResult Fail(string message) =>
        new()
        {
            Success = false,
            ErrorMessage = message
        };
}
