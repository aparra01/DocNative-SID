using DocNative.Core.Configuration;
using DocNative.Core.Paths;
using Microsoft.Extensions.Options;

namespace DocNative.Core.Tests;

public class PathLayoutTests
{
    [Fact]
    public void ResolveAgencyFromRawPath_ReturnsSucursalCode()
    {
        var layout = CreateLayout(
            rawRoot: @"C:\data\raw",
            outputRoot: @"C:\data\entrada",
            errorRoot: @"C:\data\error");

        var resolved = layout.TryResolveAgencyFromRawPath(@"C:\data\raw\QUITO\lote.pdf", out var agencia);

        Assert.True(resolved);
        Assert.Equal("QUITO", agencia);
    }

    [Fact]
    public void ResolveAgencyFromRawPath_ReturnsSinSucursal_WhenPdfInRawRoot()
    {
        var layout = CreateLayout(
            rawRoot: @"C:\data\raw",
            outputRoot: @"C:\data\entrada",
            errorRoot: @"C:\data\error");

        var resolved = layout.TryResolveAgencyFromRawPath(@"C:\data\raw\lote.pdf", out var agencia);

        Assert.True(resolved);
        Assert.Equal("SIN_SUCURSAL", agencia);
    }

    [Fact]
    public void GetAgencyErrorDirectory_UsesDateAndAgency()
    {
        var layout = CreateLayout(
            rawRoot: @"C:\data\raw",
            outputRoot: @"C:\data\entrada",
            errorRoot: @"C:\data\error");

        var path = layout.GetAgencyErrorDirectory(new DateOnly(2026, 7, 28), "GYE001");

        Assert.EndsWith(Path.Combine("error", "20260728", "GYE001"), path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    private static PathLayout CreateLayout(string rawRoot, string outputRoot, string errorRoot)
    {
        var options = Options.Create(new DocNativeOptions
        {
            RawRoot = rawRoot,
            OutputRoot = outputRoot,
            ErrorRoot = errorRoot
        });

        return new PathLayout(options);
    }
}
