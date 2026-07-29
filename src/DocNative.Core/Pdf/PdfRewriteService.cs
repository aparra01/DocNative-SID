using DocNative.Core.Abstractions;
using DocNative.Core.Models;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace DocNative.Core.Pdf;

public sealed class PdfRewriteService : IPdfRewriter
{
    public void Rewrite(string sourcePdfPath, string destinationPdfPath, IReadOnlyList<PageAnalysisResult> pages)
    {
        var directory = Path.GetDirectoryName(destinationPdfPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var input = PdfReader.Open(sourcePdfPath, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();

        foreach (var pageInfo in pages.Where(p => !p.IsBlank))
        {
            if (pageInfo.PageIndex < 0 || pageInfo.PageIndex >= input.PageCount)
            {
                continue;
            }

            var imported = output.AddPage(input.Pages[pageInfo.PageIndex]);
            imported.Rotate = NormalizeRotation(pageInfo.RotationDegrees);
        }

        if (output.PageCount == 0)
        {
            throw new InvalidOperationException("Documento sin contenido util");
        }

        var tempPath = destinationPdfPath + ".tmp";
        output.Save(tempPath);

        if (File.Exists(destinationPdfPath))
        {
            File.Delete(destinationPdfPath);
        }

        File.Move(tempPath, destinationPdfPath);
    }

    private static int NormalizeRotation(int degrees)
    {
        var normalized = degrees % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        return normalized switch
        {
            0 or 90 or 180 or 270 => normalized,
            _ => 0
        };
    }
}
