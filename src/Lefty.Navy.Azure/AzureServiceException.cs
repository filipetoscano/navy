namespace Lefty.Navy.Azure;

/// <summary />
/// <remarks>
/// Raised for conditions which the caller can act upon, and which should be
/// reported as a message rather than as a stack trace.
/// </remarks>
public class AzureServiceException : Exception
{
    /// <summary />
    public AzureServiceException( string message )
        : base( message )
    {
    }


    /// <summary />
    public AzureServiceException( string message, Exception innerException )
        : base( message, innerException )
    {
    }
}
