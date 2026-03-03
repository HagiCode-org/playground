namespace DoubaoVoice.SDK;

/// <summary>
/// Base exception for DoubaoVoice SDK
/// </summary>
public class DoubaoVoiceException : Exception
{
    public DoubaoVoiceException()
    {
    }

    public DoubaoVoiceException(string message)
        : base(message)
    {
    }

    public DoubaoVoiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when authentication fails
/// </summary>
public class AuthenticationException : DoubaoVoiceException
{
    public AuthenticationException()
    {
    }

    public AuthenticationException(string message)
        : base(message)
    {
    }

    public AuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when audio format is invalid
/// </summary>
public class InvalidAudioFormatException : DoubaoVoiceException
{
    public InvalidAudioFormatException()
    {
    }

    public InvalidAudioFormatException(string message)
        : base(message)
    {
    }

    public InvalidAudioFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when connection fails
/// </summary>
public class ConnectionException : DoubaoVoiceException
{
    public ConnectionException()
    {
    }

    public ConnectionException(string message)
        : base(message)
    {
    }

    public ConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}