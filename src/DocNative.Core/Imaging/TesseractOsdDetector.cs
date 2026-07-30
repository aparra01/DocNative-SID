using System.Diagnostics;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace DocNative.Core.Imaging;

public sealed class TesseractOsdDetector
{
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
                Arguments = BuildArguments(inputPath),
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

    private string BuildArguments(string inputPath)
    {
        return string.Join(
            ' ',
            $"\"{inputPath}\"",
            "stdout",
            "--psm 0",
            $"-c user_defined_dpi={_options.RenderDpi}",
            $"-c min_characters_to_try={_options.OsdMinCharactersToTry}",
            "-c min_orientation_margin=3");
    }

    private Mat CreateOsdImage(Mat image)
    {
        using var gray = ImageGrayHelper.ToGray(image);
        var maxEdge = Math.Max(gray.Width, gray.Height);
        var maxOsdEdge = Math.Max(500, _options.OsdMaxEdgePixels);
        if (maxEdge <= maxOsdEdge)
        {
            return gray.Clone();
        }

        var scale = maxOsdEdge / (double)maxEdge;
        var resized = new Mat();
        Cv2.Resize(
            gray,
            resized,
            new Size((int)Math.Round(gray.Width * scale), (int)Math.Round(gray.Height * scale)),
            interpolation: InterpolationFlags.Area);
        return resized;
    }
}
