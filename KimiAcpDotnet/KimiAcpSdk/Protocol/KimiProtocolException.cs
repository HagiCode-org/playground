namespace KimiAcpSdk.Protocol;

public sealed class KimiProtocolException : Exception
{
    public KimiProtocolException(string message)
        : base(message)
    {
    }

    public KimiProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
