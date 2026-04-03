namespace Finta.Common;

/// <summary>
/// Exception thrown when a parser is given a file that does not match any known structural signatures.
/// </summary>
public class UnrecognizedExchangeFormatException : Exception
{
    public string ExchangeName { get; }

    public UnrecognizedExchangeFormatException(string exchangeName) 
        : base($"The provided file is not a recognized format for {exchangeName}.")
    {
        ExchangeName = exchangeName;
    }

    public UnrecognizedExchangeFormatException(string exchangeName, string message) 
        : base(message)
    {
        ExchangeName = exchangeName;
    }
}
