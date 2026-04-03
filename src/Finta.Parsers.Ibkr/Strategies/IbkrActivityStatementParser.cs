using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Finta.Common;

namespace Finta.Parsers.Ibkr.Strategies;

/// <summary>
/// Strategy for parsing the standard IBKR "Activity Statement" CSV format.
/// </summary>
internal partial class IbkrActivityStatementParser
{
    [GeneratedRegex(@"(?<ticker>[A-Z0-9.]+)\(")]
    private static partial Regex TickerRegex();

    public async IAsyncEnumerable<Transaction> ParseAsync(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        while (await csv.ReadAsync())
        {
            var section = csv.GetField(0);
            var rowType = csv.GetField(1);

            if (rowType != "Data") continue;

            Transaction? transaction = section switch
            {
                "Trades" => ParseTrade(csv),
                "Dividends" => ParseDividend(csv),
                "Withholding Tax" => ParseTax(csv),
                "Fees" => ParseFee(csv),
                _ => null
            };

            if (transaction != null)
            {
                yield return transaction;
            }
        }
    }

    private static Transaction? ParseTrade(CsvReader csv)
    {
        var discriminator = csv.GetField(2);
        if (discriminator != "Order") return null;

        var assetCategory = csv.GetField(3);
        if (assetCategory != "Stocks") return null;

        var currency = csv.GetField(4) ?? "USD";
        var symbol = csv.GetField(5) ?? "";
        var dateTimeStr = csv.GetField(6) ?? "";
        var quantity = csv.GetField<decimal>(7);
        var price = csv.GetField<decimal>(8);
        var commission = Math.Abs(csv.GetField<decimal>(11));

        if (!DateTime.TryParseExact(dateTimeStr, "yyyy-MM-dd, HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        return new Transaction(
            Date: date,
            Ticker: symbol,
            Type: quantity > 0 ? TransactionType.Buy : TransactionType.Sell,
            Quantity: quantity,
            Price: price,
            Commission: commission,
            Currency: currency,
            RawData: string.Join(",", csv.Context?.Parser?.Record ?? Array.Empty<string>())
        );
    }

    private static Transaction? ParseDividend(CsvReader csv)
    {
        var currency = csv.GetField(2) ?? "USD";
        var dateStr = csv.GetField(3) ?? "";
        var description = csv.GetField(4) ?? "";
        var amount = csv.GetField<decimal>(5);

        if (string.IsNullOrEmpty(dateStr) || dateStr.Contains("Total")) return null;
        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out var date)) return null;

        var ticker = ExtractTicker(description);

        return new Transaction(
            Date: date,
            Ticker: ticker,
            Type: TransactionType.Dividend,
            Quantity: amount,
            Price: 1,
            Commission: 0,
            Currency: currency,
            RawData: string.Join(",", csv.Context?.Parser?.Record ?? Array.Empty<string>())
        );
    }

    private static Transaction? ParseTax(CsvReader csv)
    {
        var currency = csv.GetField(2) ?? "USD";
        var dateStr = csv.GetField(3) ?? "";
        var description = csv.GetField(4) ?? "";
        var amount = csv.GetField<decimal>(5);

        if (string.IsNullOrEmpty(dateStr) || dateStr.Contains("Total")) return null;
        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out var date)) return null;

        var ticker = ExtractTicker(description);

        return new Transaction(
            Date: date,
            Ticker: ticker,
            Type: TransactionType.Tax,
            Quantity: amount,
            Price: 1,
            Commission: 0,
            Currency: currency,
            RawData: string.Join(",", csv.Context?.Parser?.Record ?? Array.Empty<string>())
        );
    }

    private static Transaction? ParseFee(CsvReader csv)
    {
        var currency = csv.GetField(3) ?? "USD";
        var dateStr = csv.GetField(4) ?? "";
        var amount = csv.GetField<decimal>(6);

        if (string.IsNullOrEmpty(dateStr) || dateStr.Contains("Total") || amount == 0) return null;
        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out var date)) return null;

        return new Transaction(
            Date: date,
            Ticker: "FEE",
            Type: TransactionType.Fee,
            Quantity: amount,
            Price: 1,
            Commission: 0,
            Currency: currency,
            RawData: string.Join(",", csv.Context?.Parser?.Record ?? Array.Empty<string>())
        );
    }

    private static string ExtractTicker(string description)
    {
        var match = TickerRegex().Match(description);
        return match.Success ? match.Groups["ticker"].Value : "UNKNOWN";
    }
}
