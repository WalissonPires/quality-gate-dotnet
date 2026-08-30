namespace QualityGate.Application;

public sealed class ScopeException : Exception
{
    public ScopeException(string message) : base(message) { }
    public ScopeException(string message, Exception innerException) : base(message, innerException) { }
}
