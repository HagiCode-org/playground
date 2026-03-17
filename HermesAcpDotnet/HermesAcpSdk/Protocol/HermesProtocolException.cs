namespace HermesAcpSdk.Protocol;

public sealed class HermesProtocolException : Exception
{
    public HermesProtocolException(string message)
        : base(message)
    {
    }

    public HermesProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
