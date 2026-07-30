using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using DocNative.Core.Imaging;
using DocNative.Core.Models;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace DocNative.Core.Pdf;

public sealed class PdfRewriteService : IPdfRewriter
{
    private readonly DocNativeOptions _options;

    public PdfRewriteService(IOptions<DocNativeOptions> options)
    {
        _options = options.Value;
    }

    public void Rewrite(
        string sourcePdfPath,
        string destinationPdfPath,
        IReadOnlyList<PageAnalysisResult> pages,
        IReadOnlyList<Mat> pageImages,
        int renderDpi)
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

            if (ShouldBakeFromImage(pageInfo))
            {
                if (pageInfo.PageIndex >= pageImages.Count)
                {
                    continue;
                }

                AddRasterPage(output, pageImages[pageInfo.PageIndex], pageInfo, renderDpi);
                continue;
            }

            var imported = output.AddPage(input.Pages[pageInfo.PageIndex]);
            imported.Rotate = 0;
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

    private bool ShouldBakeFromImage(PageAnalysisResult pageInfo)
    {
        return pageInfo.RotationDegrees != 0
            || pageInfo.SourceRotation != 0
            || Math.Abs(pageInfo.SkewDegrees) >= _options.MinSkewDegrees;
    }

    private void AddRasterPage(PdfDocument output, Mat sourceImage, PageAnalysisResult pageInfo, int renderDpi)
    {
        using var transformed = ImageRotator.Apply(
            sourceImage,
            pageInfo.RotationDegrees,
            pageInfo.SkewDegrees,
            _options.MinSkewDegrees);
        using var bgr = new Mat();
        Cv2.CvtColor(transformed, bgr, ColorConversionCodes.BGRA2BGR);
        Cv2.ImEncode(".png", bgr, out var pngBytes);

        var dpi = renderDpi > 0 ? renderDpi : 150;
        var widthPt = transformed.Width * 72.0 / dpi;
        var heightPt = transformed.Height * 72.0 / dpi;

        var page = output.AddPage();
        page.Width = XUnit.FromPoint(widthPt);
        page.Height = XUnit.FromPoint(heightPt);

        using var gfx = XGraphics.FromPdfPage(page);
        using var xImage = XImage.FromStream(() => new MemoryStream(pngBytes));
        gfx.DrawImage(xImage, 0, 0, page.Width, page.Height);
    }
}
