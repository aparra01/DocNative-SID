namespace DocNative.Core.Abstractions;

public interface IErrorHandler
{
    Task HandleAsync(string sourcePdfPath, string agencia, string tipoError, CancellationToken cancellationToken = default);
}
