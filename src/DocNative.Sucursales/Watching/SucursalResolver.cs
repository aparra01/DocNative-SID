using DocNative.Core.Abstractions;

namespace DocNative.Sucursales.Watching;

public sealed class SucursalResolver
{
    private readonly IPathLayout _pathLayout;

    public SucursalResolver(IPathLayout pathLayout)
    {
        _pathLayout = pathLayout;
    }

    public bool TryResolve(string pdfPath, out string agencia) =>
        _pathLayout.TryResolveAgencyFromRawPath(pdfPath, out agencia);
}
