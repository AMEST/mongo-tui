namespace MongoTui.Domain.Exceptions;

/// <summary>Safe diagnostic error key; server messages and command data are deliberately omitted.</summary>
public sealed class DiagnosticsException(string key, int? code = null) : DomainException("Diagnostic operation failed.")
{
    public string Key { get; } = key;
    public int? Code { get; } = code;
}
