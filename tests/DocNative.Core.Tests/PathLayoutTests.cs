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
    public void ResolveAgencyFromStagingPath_WorksForWorkRoot()
    {
        var layout = CreateLayout();

        var resolved = layout.TryResolveAgencyFromStagingPath(
            @"C:\Users\test\AppData\Local\DocNative\work\GYE001\lote.pdf",
            out var agencia);

        Assert.True(resolved);
        Assert.Equal("GYE001", agencia);
    }

    [Fact]
    public void GetProcesandoPath_UsesWorkRootAndAgencySubfolder()
    {
        var layout = CreateLayout();

        var path = layout.GetProcesandoPath("quito", "lote.pdf");

        Assert.EndsWith(Path.Combine("work", "QUITO", "lote.pdf"), path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetListoPath_UsesEntradaAgencyListoSubfolder()
    {
        var layout = CreateLayout();

        var path = layout.GetListoPath("gye001", "lote.pdf");

        Assert.EndsWith(
            Path.Combine("entrada", "GYE001", DocNativeOptions.ListoSubfolderName, "lote.pdf"),
            path.Replace('/', '\\'),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsListoDeliveryPath_DetectsListoSubfolder()
    {
        var layout = CreateLayout();

        Assert.True(layout.IsListoDeliveryPath(@"C:\data\entrada\GYE001\LISTO\lote.pdf"));
        Assert.False(layout.IsListoDeliveryPath(@"C:\data\entrada\GYE001\lote.pdf"));
    }

    [Fact]
    public void IsIntakeEntradaPath_ExcludesListoSubfolder()
    {
        var layout = CreateLayout();

        Assert.True(layout.IsIntakeEntradaPath(@"C:\data\entrada\GYE001\lote.pdf"));
        Assert.False(layout.IsIntakeEntradaPath(@"C:\data\entrada\GYE001\LISTO\lote.pdf"));
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
    public void ResolveAgencyFromListoPath_ReturnsSucursalCode()
    {
        var layout = CreateLayout();

        var resolved = layout.TryResolveAgencyFromEntradaPath(@"C:\data\entrada\QUITO\LISTO\lote.pdf", out var agencia);

        Assert.True(resolved);
        Assert.Equal("QUITO", agencia);
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
            WorkRoot = @"C:\Users\test\AppData\Local\DocNative\work",
            SalidaRoot = @"C:\data\salida",
            ErrorRoot = string.Empty
        });

        return new PathLayout(options);
    }
}
