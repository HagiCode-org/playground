namespace IFlowSdk.Exceptions;

public class IFlowException : Exception
{
    public IFlowException(string message) : base(message)
    {
    }

    public IFlowException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class IFlowConnectionException : IFlowException
{
    public IFlowConnectionException(string message) : base(message)
    {
    }

    public IFlowConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class IFlowProtocolException : IFlowException
{
    public IFlowProtocolException(string message) : base(message)
    {
    }

    public IFlowProtocolException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class IFlowAuthenticationException : IFlowException
{
    public IFlowAuthenticationException(string message) : base(message)
    {
    }
}

public sealed class IFlowProcessException : IFlowException
{
    public IFlowProcessException(string message) : base(message)
    {
    }
}
