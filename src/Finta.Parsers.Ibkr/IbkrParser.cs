using Finta.Common;
using CsvHelper;
using System.Globalization;

namespace Finta.Parsers.Ibkr;

public class IbkrParser : IExchangeParser
{
    public string ExchangeName => "Interactive Brokers";

    public async IAsyncEnumerable<Transaction> ParseAsync(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        // This skeleton allows the project to build. 
        // Logic for filtering "Trades" rows will be the next phase.
        await Task.Yield(); 
        yield break; 
    }
}
