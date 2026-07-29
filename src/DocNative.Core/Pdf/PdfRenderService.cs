using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace DocNative.Core.Pdf;

public sealed class PdfRenderService : IPdfRenderer, IDisposable
{
    private readonly DocNativeOptions _options;
    private readonly DocLib _docLib = DocLib.Instance;

    public PdfRenderService(IOptions<DocNativeOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<Mat> RenderPages(string pdfPath, int dpi)
    {
        var renderDpi = dpi > 0 ? dpi : _options.RenderDpi;
        var scale = renderDpi / 72.0;

        using var docReader = _docLib.GetDocReader(File.ReadAllBytes(pdfPath), new PageDimensions(scale));
        var pageCount = docReader.GetPageCount();
        var pages = new List<Mat>(pageCount);

        for (var i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            var rawBytes = pageReader.GetImage();
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();

            var mat = new Mat(height, width, MatType.CV_8UC4);
            System.Runtime.InteropServices.Marshal.Copy(rawBytes, 0, mat.Data, rawBytes.Length);
            pages.Add(mat);
        }

        return pages;
    }

    public void Dispose()
    {
        _docLib.Dispose();
    }
}
