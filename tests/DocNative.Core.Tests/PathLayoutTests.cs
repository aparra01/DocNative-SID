using DocNative.Core.Configuration;
using DocNative.Core.Paths;
using Microsoft.Extensions.Options;

namespace DocNative.Core.Tests;

public class PathLayoutTests
{
    [Fact]
    public void ResolveAgencyFromEntradaPath_ReturnsSucursalCode()
    {
        var layout = CreateLayout(
            outputRoot: @"C:\data\entrada",
            salidaRoot: @"C:\data\salida",
            errorRoot: string.Empty);

        var resolved = layout.TryResolveAgencyFromEntradaPath(@"C:\data\entrada\QUITO\lote.pdf", out var agencia);

        Assert.True(resolved);
        Assert.Equal("QUITO", agencia);
    }

    [Fact]
    public void ResolveAgencyFromEntradaPath_ReturnsSinSucursal_WhenPdfInEntradaRoot()
    {
        var layout = CreateLayout(
            outputRoot: @"C:\data\entrada",
            salidaRoot: @"C:\data\salida",
            errorRoot: string.Empty);

        var resolved = layout.TryResolveAgencyFromEntradaPath(@"C:\data\entrada\lote.pdf", out var agencia);

        Assert.True(resolved);
        Assert.Equal("SIN_SUCURSAL", agencia);
    }

    [Fact]
    public void GetAgencyErrorDirectory_UsesFlatDateFolder()
    {
        var layout = CreateLayout(
            outputRoot: @"C:\data\entrada",
            salidaRoot: @"C:\data\salida",
            errorRoot: string.Empty);

        var path = layout.GetAgencyErrorDirectory(new DateOnly(2026, 7, 28), "GYE001");

        Assert.EndsWith(Path.Combine("salida", "ERROR", "28_07_2026"), path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCsvFilePath_UsesDdMmYyyyFormat()
    {
        var layout = CreateLayout(
            outputRoot: @"C:\data\entrada",
            salidaRoot: @"C:\data\salida",
            errorRoot: string.Empty);

        var path = layout.GetCsvFilePath(new DateOnly(2026, 7, 28));

        Assert.EndsWith(Path.Combine("28_07_2026", "errores_28_07_2026.csv"), path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    private static PathLayout CreateLayout(string outputRoot, string salidaRoot, string errorRoot)
    {
        var options = Options.Create(new DocNativeOptions
        {
            OutputRoot = outputRoot,
            SalidaRoot = salidaRoot,
            ErrorRoot = errorRoot
        });

        return new PathLayout(options);
    }
}
