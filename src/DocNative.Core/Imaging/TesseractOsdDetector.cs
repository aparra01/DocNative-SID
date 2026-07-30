using System.Diagnostics;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace DocNative.Core.Imaging;

public sealed class TesseractOsdDetector
{
    private const int MaxOsdEdgePixels = 1000;

    private readonly DocNativeOptions _options;

    public TesseractOsdDetector(IOptions<DocNativeOptions> options)
    {
        _options = options.Value;
    }

    internal OsdResult Detect(Mat image)
    {
        if (image.Empty())
        {
            return OsdResult.Failed("Imagen vacia");
        }

        if (string.IsNullOrWhiteSpace(_options.TesseractExecutablePath)
            || !File.Exists(_options.TesseractExecutablePath))
        {
            return OsdResult.Failed("Tesseract no encontrado");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "docnative-osd", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var inputPath = Path.Combine(tempDirectory, "page.png");

        try
        {
            using var osdImage = CreateOsdImage(image);
            Cv2.ImWrite(inputPath, osdImage);

            var startInfo = new ProcessStartInfo
            {
                FileName = _options.TesseractExecutablePath,
                Arguments = $"\"{inputPath}\" stdout --psm 0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return OsdResult.Failed("No se pudo iniciar Tesseract");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(_options.TesseractTimeoutSeconds * 1000);

            if (process.ExitCode != 0)
            {
                return OsdResult.Failed($"Tesseract exit code {process.ExitCode}", stderr);
            }

            if (!OsdOutputParser.TryParse(stdout, out var rotateDegrees, out var orientationConfidence))
            {
                return OsdResult.Failed("No se pudo parsear salida OSD", stderr);
            }

            return new OsdResult(rotateDegrees, orientationConfidence, true, null, stderr);
        }
        catch (Exception ex)
        {
            return OsdResult.Failed(ex.Message);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup for temp OSD artifacts.
            }
        }
    }

    private static Mat CreateOsdImage(Mat image)
    {
        var maxEdge = Math.Max(image.Width, image.Height);
        if (maxEdge <= MaxOsdEdgePixels)
        {
            return image.Clone();
        }

        var scale = MaxOsdEdgePixels / (double)maxEdge;
        var resized = new Mat();
        Cv2.Resize(
            image,
            resized,
            new Size((int)Math.Round(image.Width * scale), (int)Math.Round(image.Height * scale)),
            interpolation: InterpolationFlags.Area);
        return resized;
    }
}
