using DocNative.Core.Configuration;
using DocNative.Core.Paths;
using Microsoft.Extensions.Options;

namespace DocNative.Core.Tests;

public class PathLayoutTests
{
    [Fact]
    public void ResolveAgencyFromEntradaPath_ReturnsSucursalCode()
    {
        var layout = CreateLayout();

        var resolved = layout.TryResolveAgencyFromEntradaPath(@"C:\data\entrada\QUITO\lote.pdf", out var agencia);

        Assert.True(resolved);
        Assert.Equal("QUITO", agencia);
    }

    [Fact]
    public void ResolveAgencyFromStagingPath_WorksForProcesandoRoot()
    {
        var layout = CreateLayout();

        var resolved = layout.TryResolveAgencyFromStagingPath(
            @"C:\data\procesando\GYE001\lote.pdf",
            out var agencia);

        Assert.True(resolved);
        Assert.Equal("GYE001", agencia);
    }

    [Fact]
    public void GetProcesandoPath_UsesAgencySubfolder()
    {
        var layout = CreateLayout();

        var path = layout.GetProcesandoPath("quito", "lote.pdf");

        Assert.EndsWith(Path.Combine("procesando", "QUITO", "lote.pdf"), path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPreProcesadoPath_UsesAgencySubfolder()
    {
        var layout = CreateLayout();

        var path = layout.GetPreProcesadoPath("gye001", "lote.pdf");

        Assert.EndsWith(Path.Combine("preprocesado", "GYE001", "lote.pdf"), path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveAgencyFromEntradaPath_ReturnsSinSucursal_WhenPdfInEntradaRoot()
    {
        var layout = CreateLayout();

        var resolved = layout.TryResolveAgencyFromEntradaPath(@"C:\data\entrada\lote.pdf", out var agencia);

        Assert.True(resolved);
        Assert.Equal("SIN_SUCURSAL", agencia);
    }

    [Fact]
    public void GetAgencyErrorDirectory_UsesFlatDateFolder()
    {
        var layout = CreateLayout();

        var path = layout.GetAgencyErrorDirectory(new DateOnly(2026, 7, 28), "GYE001");

        Assert.EndsWith(Path.Combine("salida", "ERROR", "28_07_2026"), path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCsvFilePath_UsesDdMmYyyyFormat()
    {
        var layout = CreateLayout();

        var path = layout.GetCsvFilePath(new DateOnly(2026, 7, 28));

        Assert.EndsWith(Path.Combine("28_07_2026", "errores_28_07_2026.csv"), path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    private static PathLayout CreateLayout()
    {
        var options = Options.Create(new DocNativeOptions
        {
            OutputRoot = @"C:\data\entrada",
            ProcesandoRoot = @"C:\data\procesando",
            PreProcesadoRoot = @"C:\data\preprocesado",
            SalidaRoot = @"C:\data\salida",
            ErrorRoot = string.Empty
        });

        return new PathLayout(options);
    }
}
