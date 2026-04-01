namespace Finta.Common;

public interface IExchangeParser
{
    string ExchangeName { get; }
    
    // Process records one-by-one to keep memory usage at O(1)
    IAsyncEnumerable<Transaction> ParseAsync(Stream csvStream);
}
