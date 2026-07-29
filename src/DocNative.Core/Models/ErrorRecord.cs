namespace DocNative.Core.Models;

public sealed class ErrorRecord
{
    public int Id { get; init; }

    public DateOnly Fecha { get; init; }

    public TimeOnly Hora { get; init; }

    public string Agencia { get; init; } = string.Empty;

    public string NombrePdf { get; init; } = string.Empty;

    public string TipoError { get; init; } = string.Empty;

    public string SourcePath { get; init; } = string.Empty;

    public string DestinationPath { get; init; } = string.Empty;
}
