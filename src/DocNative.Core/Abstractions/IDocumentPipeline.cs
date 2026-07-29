using DocNative.Core.Models;

namespace DocNative.Core.Abstractions;

public interface IDocumentPipeline
{
    PipelineResult Process(string sourcePdfPath, string destinationPdfPath);
}
